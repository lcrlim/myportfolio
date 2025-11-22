#include "Common.h"
#include <random>
#include <game.pb.h>
#include <game.grpc.pb.h>

using grpc::Channel;
using grpc::ClientContext;
using grpc::Status;

class GameClient {
public:
    GameClient(std::shared_ptr<Channel> channel)
        : stub_(game::GameService::NewStub(channel)) {
    }

    // 로그인
    bool Login(const std::string& username, const std::string& password,
        std::string& out_token, int64_t& out_user_id) {
        game::LoginRequest request;
        request.set_username(username);
        request.set_password(password);
        request.set_client_version("1.0.0");
        request.set_timestamp(GetCurrentTimestamp());

        game::LoginResponse response;
        ClientContext context;

        Status status = stub_->Login(&context, request, &response);

        if (!status.ok()) {
            LOG_ERROR("Login RPC failed: " + status.error_message());
            return false;
        }

        if (response.result() == game::LoginResponse::SUCCESS) {
            out_token = response.session_token();
            out_user_id = response.user_id();
            LOG_INFO("Login successful: " + username +
                " (ID: " + std::to_string(out_user_id) + ")");
            return true;
        }
        else {
            LOG_ERROR("Login failed: " + response.message());
            return false;
        }
    }

    // 로그아웃
    bool Logout(const std::string& token, int64_t user_id) {
        game::LogoutRequest request;
        request.set_session_token(token);
        request.set_user_id(user_id);
        request.set_timestamp(GetCurrentTimestamp());

        game::LogoutResponse response;
        ClientContext context;

        Status status = stub_->Logout(&context, request, &response);

        if (!status.ok()) {
            LOG_ERROR("Logout RPC failed: " + status.error_message());
            return false;
        }

        if (response.result() == game::LogoutResponse::SUCCESS) {
            LOG_INFO("Logout successful for user ID: " + std::to_string(user_id));
            return true;
        }
        else {
            LOG_ERROR("Logout failed: " + response.message());
            return false;
        }
    }

private:
    std::unique_ptr<game::GameService::Stub> stub_;
};

// 단일 클라이언트 시나리오
void SingleClientTest(const std::string& server_address) {
    LOG_INFO("=== Single Client Test ===");

    auto channel = grpc::CreateChannel(server_address,
        grpc::InsecureChannelCredentials());
    GameClient client(channel);

    std::string token;
    int64_t user_id;

    // 로그인 테스트
    if (client.Login("testuser", "password123", token, user_id)) {
        LOG_INFO("Session Token: " + token);

        // 3초 대기
        std::this_thread::sleep_for(std::chrono::seconds(3));

        // 로그아웃 테스트
        client.Logout(token, user_id);
    }
}

// 다중 클라이언트 부하 테스트
void LoadTest(const std::string& server_address, int num_clients, int duration_seconds) {
    LOG_INFO("=== Load Test ===");
    LOG_INFO("Clients: " + std::to_string(num_clients));
    LOG_INFO("Duration: " + std::to_string(duration_seconds) + " seconds");

    std::atomic<int> success_login(0);
    std::atomic<int> failed_login(0);
    std::atomic<int> success_logout(0);
    std::atomic<int> failed_logout(0);
    std::atomic<bool> stop_flag(false);

    std::vector<std::thread> client_threads;

    auto start_time = std::chrono::steady_clock::now();

    // 클라이언트 스레드 생성
    for (int i = 0; i < num_clients; ++i) {
        client_threads.emplace_back([&, i]() {
            auto channel = grpc::CreateChannel(server_address,
                grpc::InsecureChannelCredentials());
            GameClient client(channel);

            std::random_device rd;
            std::mt19937 gen(rd());
            std::uniform_int_distribution<> sleep_dist(1, 5);

            std::string username = "user_" + std::to_string(i);
            std::string token;
            int64_t user_id;

            while (!stop_flag.load()) {
                // 로그인
                if (client.Login(username, "password123", token, user_id)) {
                    success_login.fetch_add(1);

                    // 랜덤 대기 (1-5초)
                    int sleep_time = sleep_dist(gen);
                    std::this_thread::sleep_for(std::chrono::seconds(sleep_time));

                    // 로그아웃
                    if (client.Logout(token, user_id)) {
                        success_logout.fetch_add(1);
                    }
                    else {
                        failed_logout.fetch_add(1);
                    }
                }
                else {
                    failed_login.fetch_add(1);
                }

                // 짧은 대기
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
            });
    }

    // 통계 출력 스레드
    std::thread stats_thread([&]() {
        while (!stop_flag.load()) {
            std::this_thread::sleep_for(std::chrono::seconds(5));

            auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(
                std::chrono::steady_clock::now() - start_time).count();

            LOG_INFO("=== Test Progress (" + std::to_string(elapsed) + "s) ===");
            LOG_INFO("Success Login: " + std::to_string(success_login.load()));
            LOG_INFO("Failed Login: " + std::to_string(failed_login.load()));
            LOG_INFO("Success Logout: " + std::to_string(success_logout.load()));
            LOG_INFO("Failed Logout: " + std::to_string(failed_logout.load()));
        }
        });

    // 지정된 시간만큼 대기
    std::this_thread::sleep_for(std::chrono::seconds(duration_seconds));

    // 테스트 종료
    stop_flag.store(true);
    LOG_INFO("Stopping load test...");

    // 모든 스레드 종료 대기
    for (auto& thread : client_threads) {
        if (thread.joinable()) {
            thread.join();
        }
    }

    if (stats_thread.joinable()) {
        stats_thread.join();
    }

    // 최종 통계
    LOG_INFO("=== Final Statistics ===");
    LOG_INFO("Total Success Login: " + std::to_string(success_login.load()));
    LOG_INFO("Total Failed Login: " + std::to_string(failed_login.load()));
    LOG_INFO("Total Success Logout: " + std::to_string(success_logout.load()));
    LOG_INFO("Total Failed Logout: " + std::to_string(failed_logout.load()));

    double success_rate = 0.0;
    int total_attempts = success_login.load() + failed_login.load();
    if (total_attempts > 0) {
        success_rate = (double)success_login.load() / total_attempts * 100.0;
    }
    LOG_INFO("Success Rate: " + std::to_string(success_rate) + "%");
}

// 동시 접속 테스트
void ConcurrentConnectionTest(const std::string& server_address, int num_clients) {
    LOG_INFO("=== Concurrent Connection Test ===");
    LOG_INFO("Connecting " + std::to_string(num_clients) + " clients simultaneously...");

    std::atomic<int> success_count(0);
    std::atomic<int> failed_count(0);
    std::vector<std::thread> client_threads;

    struct ClientData {
        std::string token;
        int64_t user_id;
        bool success;
    };
    std::vector<ClientData> client_data(num_clients);

    auto start_time = std::chrono::steady_clock::now();

    // 모든 클라이언트 동시 로그인
    for (int i = 0; i < num_clients; ++i) {
        client_threads.emplace_back([&, i]() {
            auto channel = grpc::CreateChannel(server_address,
                grpc::InsecureChannelCredentials());
            GameClient client(channel);

            std::string username = "concurrent_user_" + std::to_string(i);

            if (client.Login(username, "password123",
                client_data[i].token,
                client_data[i].user_id)) {
                client_data[i].success = true;
                success_count.fetch_add(1);
            }
            else {
                client_data[i].success = false;
                failed_count.fetch_add(1);
            }
            });
    }

    // 모든 로그인 완료 대기
    for (auto& thread : client_threads) {
        if (thread.joinable()) {
            thread.join();
        }
    }

    auto login_duration = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - start_time).count();

    LOG_INFO("Login Phase Complete:");
    LOG_INFO("  Success: " + std::to_string(success_count.load()));
    LOG_INFO("  Failed: " + std::to_string(failed_count.load()));
    LOG_INFO("  Time: " + std::to_string(login_duration) + "ms");

    // 10초 대기
    LOG_INFO("Waiting 10 seconds...");
    std::this_thread::sleep_for(std::chrono::seconds(10));

    // 모든 클라이언트 동시 로그아웃
    LOG_INFO("Logging out all clients...");
    client_threads.clear();
    success_count.store(0);
    failed_count.store(0);

    start_time = std::chrono::steady_clock::now();

    for (int i = 0; i < num_clients; ++i) {
        if (!client_data[i].success) continue;

        client_threads.emplace_back([&, i]() {
            auto channel = grpc::CreateChannel(server_address,
                grpc::InsecureChannelCredentials());
            GameClient client(channel);

            if (client.Logout(client_data[i].token, client_data[i].user_id)) {
                success_count.fetch_add(1);
            }
            else {
                failed_count.fetch_add(1);
            }
            });
    }

    for (auto& thread : client_threads) {
        if (thread.joinable()) {
            thread.join();
        }
    }

    auto logout_duration = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - start_time).count();

    LOG_INFO("Logout Phase Complete:");
    LOG_INFO("  Success: " + std::to_string(success_count.load()));
    LOG_INFO("  Failed: " + std::to_string(failed_count.load()));
    LOG_INFO("  Time: " + std::to_string(logout_duration) + "ms");
}

int main(int argc, char** argv) {
    std::string server_address = "localhost:50051";
    std::string test_type = "single";
    int num_clients = 100;
    int duration = 60;

    if (argc > 1) {
        server_address = argv[1];
    }
    if (argc > 2) {
        test_type = argv[2];
    }
    if (argc > 3) {
        num_clients = std::stoi(argv[3]);
    }
    if (argc > 4) {
        duration = std::stoi(argv[4]);
    }

    LOG_INFO("=== Game Client Test ===");
    LOG_INFO("Server: " + server_address);
    LOG_INFO("Test Type: " + test_type);

    try {
        if (test_type == "single") {
            SingleClientTest(server_address);
        }
        else if (test_type == "load") {
            LoadTest(server_address, num_clients, duration);
        }
        else if (test_type == "concurrent") {
            ConcurrentConnectionTest(server_address, num_clients);
        }
        else {
            LOG_ERROR("Unknown test type: " + test_type);
            LOG_INFO("Available types: single, load, concurrent");
            return 1;
        }
    }
    catch (const std::exception& e) {
        LOG_ERROR("Client error: " + std::string(e.what()));
        return 1;
    }

    LOG_INFO("Test completed");
    return 0;
}