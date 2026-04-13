// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "yieldprocessornormalized.h"
#include "minipal/time.h"

#include "finalizerthread.h"

#if defined(HOST_ARM64)
#include <minipal/cpufeatures.h>
#endif

#include "yieldprocessornormalizedshared.cpp"
