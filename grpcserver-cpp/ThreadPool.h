#pragma once

#include "Common.h"

// 고성능 스레드 풀
class ThreadPool {
public:
    explicit ThreadPool(size_t num_threads) : stop_(false) {
        for (size_t i = 0; i < num_threads; ++i) {
            workers_.emplace_back([this, i] {
                LOG_INFO("Worker thread " + std::to_string(i) + " started");
                WorkerLoop();
                });
        }
        LOG_INFO("ThreadPool initialized with " + std::to_string(num_threads) + " threads");
    }

    ~ThreadPool() {
        Shutdown();
    }

    // 태스크 추가
    template<typename F>
    void Enqueue(F&& task) {
        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            if (stop_) {
                throw std::runtime_error("ThreadPool is stopped");
            }
            tasks_.emplace(std::forward<F>(task));
        }
        cv_.notify_one();
    }

    // 종료
    void Shutdown() {
        {
            std::unique_lock<std::mutex> lock(queue_mutex_);
            if (stop_) return;
            stop_ = true;
        }

        cv_.notify_all();

        for (auto& worker : workers_) {
            if (worker.joinable()) {
                worker.join();
            }
        }

        LOG_INFO("ThreadPool shutdown complete");
    }

    // 대기 중인 태스크 수
    size_t PendingTasks() {
        std::unique_lock<std::mutex> lock(queue_mutex_);
        return tasks_.size();
    }

private:
    void WorkerLoop() {
        while (true) {
            std::function<void()> task;

            {
                std::unique_lock<std::mutex> lock(queue_mutex_);
                cv_.wait(lock, [this] { return stop_ || !tasks_.empty(); });

                if (stop_ && tasks_.empty()) {
                    return;
                }

                if (!tasks_.empty()) {
                    task = std::move(tasks_.front());
                    tasks_.pop();
                }
            }

            if (task) {
                try {
                    task();
                }
                catch (const std::exception& e) {
                    LOG_ERROR("Exception in worker thread: " + std::string(e.what()));
                }
                catch (...) {
                    LOG_ERROR("Unknown exception in worker thread");
                }
            }
        }
    }

    std::vector<std::thread> workers_;
    std::queue<std::function<void()>> tasks_;
    std::mutex queue_mutex_;
    std::condition_variable cv_;
    std::atomic<bool> stop_;
};