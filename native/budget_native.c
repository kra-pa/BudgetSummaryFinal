#include "budget_native.h"

int32_t budget_utilization_percent(int64_t planned_cents, int64_t actual_cents)
{
    if (planned_cents <= 0 || actual_cents < 0)
        return -1;

    return (int32_t)((actual_cents * 100) / planned_cents);
}
