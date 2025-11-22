#pragma once

#include <iostream>
#include <memory>
#include <string>
#include <thread>
#include <vector>
#include <queue>
#include <mutex>
#include <condition_variable>
#include <atomic>
#include <unordered_map>
#include <chrono>
#include <functional>
#include <sstream>
#include <iomanip>
#include <cassert>
#include <ctime>

// gRPC 헤더
#include <grpcpp/grpcpp.h>
#include <grpcpp/health_check_service_interface.h>
#include <grpcpp/ext/proto_server_reflection_plugin.h>
#include <grpcpp/support/status.h>

// grpc/support/log.h는 나중에 include (proto 생성 후)
// #include <grpc/support/log.h>

// Proto 생성 헤더는 Common.h를 include하는 파일에서 include해야 함
// 여기서는 전방 선언만
//namespace game {
//    class LoginRequest;
//    class LoginResponse;
//    class LogoutRequest;
//    class LogoutResponse;
//    class GameMessage;
//}

// GPR_ASSERT 매크로 정의 (없는 경우 대비)
#ifndef GPR_ASSERT
#define GPR_ASSERT(x) assert(x)
#endif

// 크로스 플랫폼 정의
#ifdef _WIN32
#define PLATFORM_WINDOWS
#define NOMINMAX  // Windows.h의 min/max 매크로 비활성화
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#undef min
#undef max
#undef ERROR  // Windows.h의 ERROR 매크로 제거
#else
#define PLATFORM_LINUX
#include <unistd.h>
#include <sys/syscall.h>
#endif

// 로깅 매크로
#define LOG_INFO(msg) Logger::GetInstance().Log(LogLevel::INFO, msg)
#define LOG_WARN(msg) Logger::GetInstance().Log(LogLevel::WARN, msg)
#define LOG_ERROR(msg) Logger::GetInstance().Log(LogLevel::ERROR, msg)
#define LOG_DEBUG(msg) Logger::GetInstance().Log(LogLevel::DEBUG, msg)

enum class LogLevel {
    DEBUG,
    INFO,
    WARN,
    ERROR
};

inline std::tm to_local_tm(std::time_t t)
{
    std::tm tm_buf{};
#ifdef _WIN32
    localtime_s(&tm_buf, &t);
#else
    localtime_r(&t, &tm_buf);
#endif
    return tm_buf;
}


// 싱글톤 로거
class Logger {
public:
    static Logger& GetInstance() {
        static Logger instance;
        return instance;
    }

    void Log(LogLevel level, const std::string& message) {
        std::lock_guard<std::mutex> lock(mutex_);

        auto now = std::chrono::system_clock::now();
        auto time = std::chrono::system_clock::to_time_t(now);
        auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            now.time_since_epoch()) % 1000;

        std::stringstream ss;
        auto tm_buf = to_local_tm(time);
        ss << "[" << std::put_time(&tm_buf, "%Y-%m-%d %H:%M:%S")
            << "." << std::setfill('0') << std::setw(3) << ms.count() << "] "
            << "[" << GetLevelString(level) << "] "
            << "[TID:" << GetThreadId() << "] "
            << message << std::endl;

        std::cout << ss.str();
    }

private:
    Logger() = default;
    std::mutex mutex_;

    std::string GetLevelString(LogLevel level) {
        switch (level) {
        case LogLevel::DEBUG: return "DEBUG";
        case LogLevel::INFO:  return "INFO ";
        case LogLevel::WARN:  return "WARN ";
        case LogLevel::ERROR: return "ERROR";
        default: return "UNKNOWN";
        }
    }

    uint64_t GetThreadId() {
#ifdef PLATFORM_WINDOWS
        return static_cast<uint64_t>(GetCurrentThreadId());
#else
        return static_cast<uint64_t>(syscall(SYS_gettid));
#endif
    }
};

// Lock-free Concurrent Queue (SPSC)
template<typename T>
class ConcurrentQueue {
public:
    ConcurrentQueue(size_t capacity = 10000) : capacity_(capacity), head_(0), tail_(0) {
        buffer_.resize(capacity);
    }

    bool Push(const T& item) {
        size_t current_tail = tail_.load(std::memory_order_relaxed);
        size_t next_tail = (current_tail + 1) % capacity_;

        if (next_tail == head_.load(std::memory_order_acquire)) {
            return false; // Queue full
        }

        buffer_[current_tail] = item;
        tail_.store(next_tail, std::memory_order_release);
        return true;
    }

    bool Pop(T& item) {
        size_t current_head = head_.load(std::memory_order_relaxed);

        if (current_head == tail_.load(std::memory_order_acquire)) {
            return false; // Queue empty
        }

        item = std::move(buffer_[current_head]);
        head_.store((current_head + 1) % capacity_, std::memory_order_release);
        return true;
    }

    size_t Size() const {
        size_t h = head_.load(std::memory_order_acquire);
        size_t t = tail_.load(std::memory_order_acquire);
        return (t >= h) ? (t - h) : (capacity_ - h + t);
    }

private:
    std::vector<T> buffer_;
    size_t capacity_;
    std::atomic<size_t> head_;
    std::atomic<size_t> tail_;
};

// 세션 정보
struct Session {
    int64_t user_id;
    std::string username;
    std::string session_token;
    std::chrono::steady_clock::time_point last_activity;
    std::atomic<bool> is_active;

    Session() : user_id(0), is_active(false) {}
    Session(int64_t id, const std::string& name, const std::string& token)
        : user_id(id), username(name), session_token(token),
        last_activity(std::chrono::steady_clock::now()), is_active(true) {
    }
};

// 유틸리티 함수
inline std::string GenerateSessionToken(int64_t user_id) {
    auto now = std::chrono::system_clock::now().time_since_epoch().count();
    std::stringstream ss;
    ss << "SESSION_" << user_id << "_" << now;
    return ss.str();
}

inline int64_t GetCurrentTimestamp() {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
}