// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "yieldprocessornormalized.h"

bool YieldProcessorNormalization::s_isMeasurementScheduled;

// Defaults are for when normalization has not yet been done
unsigned int YieldProcessorNormalization::s_yieldsPerNormalizedYield = 1;
unsigned int YieldProcessorNormalization::s_optimalMaxNormalizedYieldsPerSpinIteration =
    (unsigned int)
    (
        (double)YieldProcessorNormalization::TargetMaxNsPerSpinIteration /
        YieldProcessorNormalization::TargetNsPerNormalizedYield +
        0.5
    );

#if defined(HOST_ARM64)
bool g_arm64UseWfet = false;
uint32_t g_arm64WfetDelayNs = YieldProcessorNormalization::TargetNsPerNormalizedYield;
#endif
