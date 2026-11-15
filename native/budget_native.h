#ifndef BUDGET_NATIVE_H
#define BUDGET_NATIVE_H

#include <stdint.h>

#ifdef _WIN32
#define BUDGET_API __declspec(dllexport)
#else
#define BUDGET_API
#endif

/* Returns floor(actual_cents * 100 / planned_cents), or -1 for invalid input. */
BUDGET_API int32_t budget_utilization_percent(int64_t planned_cents, int64_t actual_cents);

#endif
