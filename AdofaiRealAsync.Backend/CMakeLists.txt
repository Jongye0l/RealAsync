cmake_minimum_required(VERSION 3.29)
project(AdofaiRealAsync.Backend)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -static")

add_executable(AdofaiRealAsync.Backend main.cpp resource.rc)
