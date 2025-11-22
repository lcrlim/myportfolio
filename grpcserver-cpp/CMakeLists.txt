# cmake path - D:/work/Dev/vcpkg/scripts/buildsystems/vcpkg.cmake

cmake_minimum_required(VERSION 3.15)
project(GameServer CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 플랫폼별 컴파일 옵션
if(MSVC)
    add_compile_options(/W4 /std:c++17)
    add_definitions(-D_WIN32_WINNT=0x0600)
    add_definitions(-DNOMINMAX)  # Windows min/max 매크로 비활성화
    add_definitions(-DWIN32_LEAN_AND_MEAN)
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)  # localtime 경고 제거
    # 빌드 타입별 최적화 옵션
    set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} /O2")
    set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} /Od /Zi")
else()
    add_compile_options(-Wall -Wextra -pthread)
    set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -O3")
    set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -O0 -g")
endif()

# gRPC 및 Protobuf 찾기
find_package(Protobuf CONFIG REQUIRED)
find_package(gRPC CONFIG REQUIRED)

message(STATUS "===========================================")
message(STATUS "Protobuf version: ${Protobuf_VERSION}")
message(STATUS "gRPC version: ${gRPC_VERSION}")
message(STATUS "C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "CXX Compiler: ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
message(STATUS "CXX Flags: ${CMAKE_CXX_FLAGS}")
message(STATUS "===========================================")

# Proto 생성 경로 설정
set(PROTO_PATH "${CMAKE_CURRENT_SOURCE_DIR}")
set(PROTO_FILE "${CMAKE_CURRENT_SOURCE_DIR}/game.proto")
set(GENERATED_PROTOBUF_PATH "${CMAKE_CURRENT_BINARY_DIR}/generated")

# 생성될 파일들
set(PROTO_SRCS "${GENERATED_PROTOBUF_PATH}/game.pb.cc")
set(PROTO_HDRS "${GENERATED_PROTOBUF_PATH}/game.pb.h")
set(GRPC_SRCS "${GENERATED_PROTOBUF_PATH}/game.grpc.pb.cc")
set(GRPC_HDRS "${GENERATED_PROTOBUF_PATH}/game.grpc.pb.h")

# 생성 디렉토리 생성
file(MAKE_DIRECTORY ${GENERATED_PROTOBUF_PATH})

# protoc 및 grpc_cpp_plugin 경로
get_target_property(PROTOBUF_PROTOC_EXECUTABLE protobuf::protoc IMPORTED_LOCATION_RELEASE)
if(NOT PROTOBUF_PROTOC_EXECUTABLE)
    get_target_property(PROTOBUF_PROTOC_EXECUTABLE protobuf::protoc IMPORTED_LOCATION_DEBUG)
endif()

get_target_property(GRPC_CPP_PLUGIN gRPC::grpc_cpp_plugin IMPORTED_LOCATION_RELEASE)
if(NOT GRPC_CPP_PLUGIN)
    get_target_property(GRPC_CPP_PLUGIN gRPC::grpc_cpp_plugin IMPORTED_LOCATION_DEBUG)
endif()

message(STATUS "protoc: ${PROTOBUF_PROTOC_EXECUTABLE}")
message(STATUS "grpc_cpp_plugin: ${GRPC_CPP_PLUGIN}")

# Proto 컴파일 커스텀 커맨드
add_custom_command(
    OUTPUT ${PROTO_SRCS} ${PROTO_HDRS} ${GRPC_SRCS} ${GRPC_HDRS}
    COMMAND ${PROTOBUF_PROTOC_EXECUTABLE}
    ARGS --grpc_out=${GENERATED_PROTOBUF_PATH}
         --cpp_out=${GENERATED_PROTOBUF_PATH}
         -I${PROTO_PATH}
         --plugin=protoc-gen-grpc=${GRPC_CPP_PLUGIN}
         ${PROTO_FILE}
    DEPENDS ${PROTO_FILE}
    COMMENT "Generating protobuf and gRPC C++ files from ${PROTO_FILE}"
    VERBATIM
)

# Proto 라이브러리 생성
add_library(game_proto STATIC
    ${PROTO_SRCS}
    ${PROTO_HDRS}
    ${GRPC_SRCS}
    ${GRPC_HDRS}
)

target_link_libraries(game_proto
    PUBLIC
        protobuf::libprotobuf
        gRPC::grpc++
)

# CRITICAL: include 경로를 PUBLIC으로 설정
target_include_directories(game_proto 
    PUBLIC 
        ${GENERATED_PROTOBUF_PATH}
)

# 생성 파일 확인을 위한 커스텀 타겟
add_custom_target(check_generated_files
    COMMAND ${CMAKE_COMMAND} -E echo "Checking generated files..."
    COMMAND ${CMAKE_COMMAND} -E echo "PROTO_SRCS: ${PROTO_SRCS}"
    COMMAND ${CMAKE_COMMAND} -E echo "PROTO_HDRS: ${PROTO_HDRS}"
    COMMAND ${CMAKE_COMMAND} -E echo "GRPC_SRCS: ${GRPC_SRCS}"
    COMMAND ${CMAKE_COMMAND} -E echo "GRPC_HDRS: ${GRPC_HDRS}"
    DEPENDS game_proto
)

# 서버 실행 파일
add_executable(game_server
    GameServer.cpp
)

# 의존성 명시 (proto 파일이 먼저 생성되도록)
add_dependencies(game_server game_proto)

target_include_directories(game_server
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${GENERATED_PROTOBUF_PATH}
)

target_link_libraries(game_server
    PRIVATE
        game_proto
        gRPC::grpc++
        gRPC::grpc++_reflection
        protobuf::libprotobuf
)

if(WIN32)
    target_link_libraries(game_server PRIVATE ws2_32)
else()
    target_link_libraries(game_server PRIVATE pthread)
endif()

# 클라이언트 실행 파일
add_executable(test_client
    TestClient.cpp
)

add_dependencies(test_client game_proto)

target_include_directories(test_client
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${GENERATED_PROTOBUF_PATH}
)

target_link_libraries(test_client
    PRIVATE
        game_proto
        gRPC::grpc++
        protobuf::libprotobuf
)

if(WIN32)
    target_link_libraries(test_client PRIVATE ws2_32)
else()
    target_link_libraries(test_client PRIVATE pthread)
endif()

# 인스톨 설정
install(TARGETS game_server test_client
    RUNTIME DESTINATION bin
)

# 빌드 정보 출력
message(STATUS "========================================")
message(STATUS "GameServer Build Configuration")
message(STATUS "========================================")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Source dir: ${CMAKE_CURRENT_SOURCE_DIR}")
message(STATUS "Binary dir: ${CMAKE_CURRENT_BINARY_DIR}")
message(STATUS "Generated proto path: ${GENERATED_PROTOBUF_PATH}")
message(STATUS "========================================")