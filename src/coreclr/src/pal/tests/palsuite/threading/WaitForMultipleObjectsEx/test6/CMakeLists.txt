cmake_minimum_required(VERSION 2.8.12.2)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

set(TESTSOURCES
  test6.cpp
)

add_executable(paltest_waitformultipleobjectsex_test6
  ${TESTSOURCES}
)

add_dependencies(paltest_waitformultipleobjectsex_test6 coreclrpal)

target_link_libraries(paltest_waitformultipleobjectsex_test6
  ${COMMON_TEST_LIBRARIES}
)


set(HELPERSOURCES
  child6.cpp
)

add_executable(paltest_waitformultipleobjectsex_test6_child
  ${HELPERSOURCES}
)

add_dependencies(paltest_waitformultipleobjectsex_test6_child coreclrpal)

target_link_libraries(paltest_waitformultipleobjectsex_test6_child
  ${COMMON_TEST_LIBRARIES}
)
