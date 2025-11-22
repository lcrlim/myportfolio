#include "Common.h"
#include "SessionManager.h"
#include "ThreadPool.h"
#include "GameServiceImpl.h"
#include <game.grpc.pb.h>

using grpc::Server;
using grpc::ServerBuilder;
using grpc::ServerContext;
using grpc::ServerAsyncResponseWriter;
using grpc::CompletionQueue;
using grpc::ServerCompletionQueue;
using grpc::Status;

// 비동기 RPC 핸들러 베이스 클래스
class CallDataBase {
public:
    virtual void Proceed() = 0;
    virtual ~CallDataBase() = default;
};

// 로그인 RPC 핸들러
class LoginCallData : public CallDataBase {
public:
    LoginCallData(game::GameService::AsyncService* service,
        ServerCompletionQueue* cq,
        GameServiceImpl* impl)
        : service_(service), cq_(cq), responder_(&ctx_), impl_(impl), status_(CREATE) {
        Proceed();
    }

    void Proceed() override {
        if (status_ == CREATE) {
            status_ = PROCESS;
            service_->RequestLogin(&ctx_, &request_, &responder_, cq_, cq_, this);
        }
        else if (status_ == PROCESS) {
            // 새로운 요청을 받기 위해 새 핸들러 생성
            new LoginCallData(service_, cq_, impl_);

            // 비즈니스 로직 처리
            impl_->HandleLogin(&request_, &response_, [this]() {
                status_ = FINISH;
                responder_.Finish(response_, Status::OK, this);
                });
        }
        else {
            assert(status_ == FINISH);
            delete this;
        }
    }

private:
    game::GameService::AsyncService* service_;
    ServerCompletionQueue* cq_;
    ServerContext ctx_;
    game::LoginRequest request_;
    game::LoginResponse response_;
    ServerAsyncResponseWriter<game::LoginResponse> responder_;
    GameServiceImpl* impl_;

    enum CallStatus { CREATE, PROCESS, FINISH };
    CallStatus status_;
};

// 로그아웃 RPC 핸들러
class LogoutCallData : public CallDataBase {
public:
    LogoutCallData(game::GameService::AsyncService* service,
        ServerCompletionQueue* cq,
        GameServiceImpl* impl)
        : service_(service), cq_(cq), responder_(&ctx_), impl_(impl), status_(CREATE) {
        Proceed();
    }

    void Proceed() override {
        if (status_ == CREATE) {
            status_ = PROCESS;
            service_->RequestLogout(&ctx_, &request_, &responder_, cq_, cq_, this);
        }
        else if (status_ == PROCESS) {
            new LogoutCallData(service_, cq_, impl_);

            impl_->HandleLogout(&request_, &response_, [this]() {
                status_ = FINISH;
                responder_.Finish(response_, Status::OK, this);
                });
        }
        else {
            GPR_ASSERT(status_ == FINISH);
            delete this;
        }
    }

private:
    game::GameService::AsyncService* service_;
    ServerCompletionQueue* cq_;
    ServerContext ctx_;
    game::LogoutRequest request_;
    game::LogoutResponse response_;
    ServerAsyncResponseWriter<game::LogoutResponse> responder_;
    GameServiceImpl* impl_;

    enum CallStatus { CREATE, PROCESS, FINISH };
    CallStatus status_;
};

// 게임 서버 클래스
class GameServer {
public:
    GameServer(const std::string& server_address, int num_cq_threads, int num_workers)
        : server_address_(server_address),
        num_cq_threads_(num_cq_threads),
        worker_pool_(num_workers),
        service_impl_(&worker_pool_),
        shutdown_(false) {

        LOG_INFO("=== Game Server Configuration ===");
        LOG_INFO("Address: " + server_address_);
        LOG_INFO("CQ Threads: " + std::to_string(num_cq_threads_));
        LOG_INFO("Worker Threads: " + std::to_string(num_workers));
        LOG_INFO("Max Sessions: " + std::to_string(SessionManager::MAX_SESSIONS));
    }

    ~GameServer() {
        Shutdown();
    }

    void Run() {
        ServerBuilder builder;

        // 서버 옵션 설정 (고성능)
        builder.SetMaxReceiveMessageSize(4 * 1024 * 1024); // 4MB
        builder.SetMaxSendMessageSize(4 * 1024 * 1024);
        builder.AddListeningPort(server_address_, grpc::InsecureServerCredentials());
        builder.RegisterService(&service_);

        // Completion Queue 생성
        for (int i = 0; i < num_cq_threads_; ++i) {
            cqs_.emplace_back(builder.AddCompletionQueue());
        }

        server_ = builder.BuildAndStart();
        LOG_INFO("Server listening on " + server_address_);

        // 각 CQ마다 초기 RPC 핸들러 생성
        for (auto& cq : cqs_) {
            new LoginCallData(&service_, cq.get(), &service_impl_);
            new LogoutCallData(&service_, cq.get(), &service_impl_);
        }

        // CQ 처리 스레드 시작
        for (int i = 0; i < num_cq_threads_; ++i) {
            cq_threads_.emplace_back([this, i]() {
                HandleRpcs(i);
                });
        }

        // 세션 타임아웃 체크 스레드
        timeout_thread_ = std::thread([this]() {
            TimeoutChecker();
            });

        // 통계 출력 스레드
        stats_thread_ = std::thread([this]() {
            PrintStats();
            });

        LOG_INFO("=== Server Started Successfully ===");
    }

    void Wait() {
        for (auto& thread : cq_threads_) {
            if (thread.joinable()) {
                thread.join();
            }
        }
        if (timeout_thread_.joinable()) {
            timeout_thread_.join();
        }
        if (stats_thread_.joinable()) {
            stats_thread_.join();
        }
    }

    void Shutdown() {
        if (shutdown_.exchange(true)) {
            return;
        }

        LOG_INFO("Shutting down server...");

        if (server_) {
            server_->Shutdown();
        }

        for (auto& cq : cqs_) {
            cq->Shutdown();
        }

        Wait();
        LOG_INFO("Server shutdown complete");
    }

private:
    void HandleRpcs(int thread_id) {
        LOG_INFO("CQ thread " + std::to_string(thread_id) + " started");

        void* tag;
        bool ok;
        auto& cq = cqs_[thread_id];

        while (cq->Next(&tag, &ok)) {
            if (ok) {
                static_cast<CallDataBase*>(tag)->Proceed();
            }
            else {
                LOG_WARN("RPC failed in CQ thread " + std::to_string(thread_id));
            }
        }

        LOG_INFO("CQ thread " + std::to_string(thread_id) + " stopped");
    }

    void TimeoutChecker() {
        LOG_INFO("Timeout checker thread started");

        while (!shutdown_.load()) {
            std::this_thread::sleep_for(std::chrono::seconds(30));

            // 5분 동안 활동이 없으면 타임아웃
            SessionManager::GetInstance().CheckTimeouts(std::chrono::seconds(300));
        }

        LOG_INFO("Timeout checker thread stopped");
    }

    void PrintStats() {
        LOG_INFO("Statistics thread started");

        while (!shutdown_.load()) {
            std::this_thread::sleep_for(std::chrono::seconds(10));

            size_t active_sessions = SessionManager::GetInstance().GetActiveSessionCount();
            size_t pending_tasks = worker_pool_.PendingTasks();

            LOG_INFO("=== Server Stats ===");
            LOG_INFO("Active Sessions: " + std::to_string(active_sessions) +
                " / " + std::to_string(SessionManager::MAX_SESSIONS));
            LOG_INFO("Pending Tasks: " + std::to_string(pending_tasks));
        }

        LOG_INFO("Statistics thread stopped");
    }

    std::string server_address_;
    int num_cq_threads_;

    game::GameService::AsyncService service_;
    std::unique_ptr<Server> server_;
    std::vector<std::unique_ptr<ServerCompletionQueue>> cqs_;

    ThreadPool worker_pool_;
    GameServiceImpl service_impl_;

    std::vector<std::thread> cq_threads_;
    std::thread timeout_thread_;
    std::thread stats_thread_;
    std::atomic<bool> shutdown_;
};

int main(int argc, char** argv) {
    std::string server_address = "0.0.0.0:50051";
    int num_cq_threads = std::thread::hardware_concurrency();
    int num_workers = std::thread::hardware_concurrency() * 2;

    if (argc > 1) {
        server_address = argv[1];
    }
    if (argc > 2) {
        num_cq_threads = std::stoi(argv[2]);
    }
    if (argc > 3) {
        num_workers = std::stoi(argv[3]);
    }

    LOG_INFO("Starting Game Server...");

    try {
        GameServer server(server_address, num_cq_threads, num_workers);
        server.Run();

        // Ctrl+C 대기
        std::cout << "Press Enter to stop the server..." << std::endl;
        std::cin.get();

        server.Shutdown();
    }
    catch (const std::exception& e) {
        LOG_ERROR("Server error: " + std::string(e.what()));
        return 1;
    }

    LOG_INFO("Server terminated");
    return 0;
}