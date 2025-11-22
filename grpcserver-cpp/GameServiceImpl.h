#pragma once

#include "Common.h"
#include "SessionManager.h"
#include "ThreadPool.h"
#include <game.pb.h>

// 비동기 gRPC 서비스 구현
class GameServiceImpl final {
public:
    GameServiceImpl(ThreadPool* worker_pool)
        : worker_pool_(worker_pool), next_user_id_(1) {
        LOG_INFO("GameServiceImpl initialized");
    }

    ~GameServiceImpl() {
        LOG_INFO("GameServiceImpl destroyed");
    }

    // 로그인 처리
    void HandleLogin(const game::LoginRequest* request,
        game::LoginResponse* response,
        const std::function<void()>& finish_callback) {

        // 워커 스레드에서 비즈니스 로직 처리
        worker_pool_->Enqueue([this, request, response, finish_callback]() {
            LOG_DEBUG("Processing login request for: " + request->username());

            // 유효성 검증
            if (request->username().empty() || request->password().empty()) {
                response->set_result(game::LoginResponse::INVALID_CREDENTIALS);
                response->set_message("Username or password is empty");
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            // 간단한 인증 (실제로는 DB 조회 필요)
            if (request->password() != "password123") {
                response->set_result(game::LoginResponse::INVALID_CREDENTIALS);
                response->set_message("Invalid credentials");
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            // 유저 ID 생성
            int64_t user_id = next_user_id_.fetch_add(1, std::memory_order_relaxed);

            // 세션 생성
            std::string session_token;
            if (!SessionManager::GetInstance().Login(request->username(),
                user_id,
                session_token)) {
                // 서버 가득 참 또는 중복 로그인
                if (SessionManager::GetInstance().GetActiveSessionCount() >=
                    SessionManager::MAX_SESSIONS) {
                    response->set_result(game::LoginResponse::SERVER_FULL);
                    response->set_message("Server is full. Please try again later.");
                }
                else {
                    response->set_result(game::LoginResponse::ALREADY_LOGGED_IN);
                    response->set_message("User is already logged in");
                }
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            // 성공 응답
            response->set_result(game::LoginResponse::SUCCESS);
            response->set_message("Login successful");
            response->set_session_token(session_token);
            response->set_user_id(user_id);
            response->set_timestamp(GetCurrentTimestamp());

            LOG_INFO("Login successful: " + request->username() +
                " (ID: " + std::to_string(user_id) + ")");

            finish_callback();
            });
    }

    // 로그아웃 처리
    void HandleLogout(const game::LogoutRequest* request,
        game::LogoutResponse* response,
        const std::function<void()>& finish_callback) {

        worker_pool_->Enqueue([this, request, response, finish_callback]() {
            LOG_DEBUG("Processing logout request for user: " +
                std::to_string(request->user_id()));

            // 세션 검증
            auto session = SessionManager::GetInstance().GetSessionByToken(
                request->session_token());

            if (!session) {
                response->set_result(game::LogoutResponse::INVALID_SESSION);
                response->set_message("Invalid session token");
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            if (session->user_id != request->user_id()) {
                response->set_result(game::LogoutResponse::INVALID_SESSION);
                response->set_message("User ID mismatch");
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            // 로그아웃 처리
            if (!SessionManager::GetInstance().Logout(request->session_token())) {
                response->set_result(game::LogoutResponse::NOT_LOGGED_IN);
                response->set_message("User is not logged in");
                response->set_timestamp(GetCurrentTimestamp());
                finish_callback();
                return;
            }

            // 성공 응답
            response->set_result(game::LogoutResponse::SUCCESS);
            response->set_message("Logout successful");
            response->set_timestamp(GetCurrentTimestamp());

            LOG_INFO("Logout successful: " + session->username);

            finish_callback();
            });
    }

private:
    ThreadPool* worker_pool_;
    std::atomic<int64_t> next_user_id_;
};