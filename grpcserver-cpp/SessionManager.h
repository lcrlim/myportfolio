#pragma once

#include "Common.h"
#include <shared_mutex>

// 고성능 세션 관리자
class SessionManager {
public:
    static SessionManager& GetInstance() {
        static SessionManager instance;
        return instance;
    }

    // 최대 동시 접속자 수
    static constexpr size_t MAX_SESSIONS = 30000;

    // 로그인 처리
    bool Login(const std::string& username, int64_t user_id, std::string& out_token) {
        // 동시 접속자 수 체크
        if (active_sessions_.load(std::memory_order_acquire) >= MAX_SESSIONS) {
            LOG_WARN("Server is full. Active sessions: " +
                std::to_string(active_sessions_.load()));
            return false;
        }

        // 중복 로그인 체크
        {
            std::shared_lock<std::shared_mutex> lock(username_mutex_);
            if (username_to_userid_.find(username) != username_to_userid_.end()) {
                LOG_WARN("User already logged in: " + username);
                return false;
            }
        }

        // 세션 생성
        std::string token = GenerateSessionToken(user_id);
        auto session = std::make_shared<Session>(user_id, username, token);

        // 세션 등록
        {
            std::unique_lock<std::shared_mutex> lock1(token_mutex_);
            std::unique_lock<std::shared_mutex> lock2(userid_mutex_);
            std::unique_lock<std::shared_mutex> lock3(username_mutex_);

            token_to_session_[token] = session;
            userid_to_session_[user_id] = session;
            username_to_userid_[username] = user_id;
        }

        active_sessions_.fetch_add(1, std::memory_order_release);
        out_token = token;

        LOG_INFO("User logged in: " + username + " (ID: " +
            std::to_string(user_id) + ") Total: " +
            std::to_string(active_sessions_.load()));
        return true;
    }

    // 로그아웃 처리
    bool Logout(const std::string& token) {
        std::shared_ptr<Session> session;

        // 세션 조회
        {
            std::shared_lock<std::shared_mutex> lock(token_mutex_);
            auto it = token_to_session_.find(token);
            if (it == token_to_session_.end()) {
                LOG_WARN("Invalid session token for logout");
                return false;
            }
            session = it->second;
        }

        if (!session->is_active.load(std::memory_order_acquire)) {
            LOG_WARN("Session already inactive: " + session->username);
            return false;
        }

        // 세션 비활성화
        session->is_active.store(false, std::memory_order_release);

        // 세션 제거
        {
            std::unique_lock<std::shared_mutex> lock1(token_mutex_);
            std::unique_lock<std::shared_mutex> lock2(userid_mutex_);
            std::unique_lock<std::shared_mutex> lock3(username_mutex_);

            token_to_session_.erase(token);
            userid_to_session_.erase(session->user_id);
            username_to_userid_.erase(session->username);
        }

        active_sessions_.fetch_sub(1, std::memory_order_release);

        LOG_INFO("User logged out: " + session->username +
            " Total: " + std::to_string(active_sessions_.load()));
        return true;
    }

    // 토큰으로 세션 조회
    std::shared_ptr<Session> GetSessionByToken(const std::string& token) {
        std::shared_lock<std::shared_mutex> lock(token_mutex_);
        auto it = token_to_session_.find(token);
        return (it != token_to_session_.end()) ? it->second : nullptr;
    }

    // 유저 ID로 세션 조회
    std::shared_ptr<Session> GetSessionByUserId(int64_t user_id) {
        std::shared_lock<std::shared_mutex> lock(userid_mutex_);
        auto it = userid_to_session_.find(user_id);
        return (it != userid_to_session_.end()) ? it->second : nullptr;
    }

    // 활성 세션 수
    size_t GetActiveSessionCount() const {
        return active_sessions_.load(std::memory_order_acquire);
    }

    // 세션 타임아웃 체크 (백그라운드 스레드에서 호출)
    void CheckTimeouts(std::chrono::seconds timeout_duration) {
        std::vector<std::string> timeout_tokens;
        auto now = std::chrono::steady_clock::now();

        {
            std::shared_lock<std::shared_mutex> lock(token_mutex_);
            for (const auto& [token, session] : token_to_session_) {
                if (session->is_active.load(std::memory_order_acquire)) {
                    auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(
                        now - session->last_activity);
                    if (elapsed > timeout_duration) {
                        timeout_tokens.push_back(token);
                    }
                }
            }
        }

        // 타임아웃된 세션 제거
        for (const auto& token : timeout_tokens) {
            LOG_WARN("Session timeout: " + token);
            Logout(token);
        }
    }

    // 세션 활동 갱신
    void UpdateActivity(const std::string& token) {
        auto session = GetSessionByToken(token);
        if (session && session->is_active.load(std::memory_order_acquire)) {
            session->last_activity = std::chrono::steady_clock::now();
        }
    }

private:
    SessionManager() : active_sessions_(0) {}

    std::unordered_map<std::string, std::shared_ptr<Session>> token_to_session_;
    std::unordered_map<int64_t, std::shared_ptr<Session>> userid_to_session_;
    std::unordered_map<std::string, int64_t> username_to_userid_;

    mutable std::shared_mutex token_mutex_;
    mutable std::shared_mutex userid_mutex_;
    mutable std::shared_mutex username_mutex_;

    std::atomic<size_t> active_sessions_;
};