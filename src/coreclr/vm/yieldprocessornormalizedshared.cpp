// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

enum class NormalizationState : uint8_t
{
    Uninitialized,
    Initialized,
    Failed
};

// Yield type selector for ARM64 spin-wait.
// Configurable via DOTNET_Arm64YieldType environment variable (available in Release builds).
//   0 = Auto: use WFET if FEAT_WFxT is available, otherwise YIELD
//   1 = YIELD: always use the YIELD instruction (baseline, essentially NOP on most ARM64)
//   2 = WFET: use Wait-For-Event-with-Timeout (requires FEAT_WFxT + FEAT_ECV, ARMv8.7+)
enum class Arm64YieldType : uint8_t
{
    Auto  = 0,
    Yield = 1,
    Wfet  = 2,
};

static const int NsPerYieldMeasurementCount = 8;
static const int64_t MeasurementPeriodMs = 4000;

static const unsigned int NsPerS = 1000 * 1000 * 1000;

static NormalizationState s_normalizationState = NormalizationState::Uninitialized;
static int64_t s_previousNormalizationTimeMs;

static int64_t s_performanceCounterTicksPerS;
static double s_nsPerYieldMeasurements[NsPerYieldMeasurementCount];
static int s_nextMeasurementIndex;
static double s_establishedNsPerYield = YieldProcessorNormalization::TargetNsPerNormalizedYield;

// When WFET is active, we need the raw YIELD measurement for the GC scaling factor.
// The GC uses its own YieldProcessor() (raw YIELD), not System_YieldProcessor().
#if defined(HOST_ARM64)
static double s_rawYieldNsPerYield = YieldProcessorNormalization::TargetNsPerNormalizedYield;
#endif

void RhEnableFinalization();

static unsigned int DetermineMeasureDurationUs()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
#ifndef FEATURE_NATIVEAOT
        MODE_PREEMPTIVE;
#endif
    }
    CONTRACTL_END;

    _ASSERTE(s_normalizationState != NormalizationState::Failed);

    // On some systems, querying the high performance counter has relatively significant overhead. Increase the measure duration
    // if the overhead seems high relative to the measure duration.
    unsigned int measureDurationUs = 1;
    int64_t startTicks = minipal_hires_ticks();
    int64_t elapsedTicks = minipal_hires_ticks() - startTicks;
    if (elapsedTicks >= s_performanceCounterTicksPerS * measureDurationUs * (1000 / 4) / NsPerS) // elapsed >= 1/4 of the measure duration
    {
        measureDurationUs *= 4;
    }
    return measureDurationUs;
}

static double MeasureNsPerYield(unsigned int measureDurationUs)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
#ifndef FEATURE_NATIVEAOT
        MODE_PREEMPTIVE;
#endif
    }
    CONTRACTL_END;

    _ASSERTE(s_normalizationState != NormalizationState::Failed);

    int yieldCount = (int)(measureDurationUs * 1000 / s_establishedNsPerYield) + 1;
    int64_t ticksPerS = s_performanceCounterTicksPerS;
    int64_t measureDurationTicks = ticksPerS * measureDurationUs / (1000 * 1000);

    int64_t startTicks = minipal_hires_ticks();

    for (int i = 0; i < yieldCount; ++i)
    {
        System_YieldProcessor();
    }

    int64_t elapsedTicks = minipal_hires_ticks() - startTicks;
    while (elapsedTicks < measureDurationTicks)
    {
        int nextYieldCount =
            max(4,
                elapsedTicks == 0
                    ? yieldCount / 4
                    : (int)(yieldCount * (measureDurationTicks - elapsedTicks) / (double)elapsedTicks) + 1);
        for (int i = 0; i < nextYieldCount; ++i)
        {
            System_YieldProcessor();
        }

        elapsedTicks = minipal_hires_ticks() - startTicks;
        yieldCount += nextYieldCount;
    }

    // Limit the minimum to a reasonable value considering that on some systems a yield may be implemented as a no-op
    const double MinNsPerYield = 0.1;

    // Measured values higher than this don't affect values calculated for normalization, and it's very unlikely for a yield to
    // really take this long. Limit the maximum to keep the recorded values reasonable.
    const double MaxNsPerYield = YieldProcessorNormalization::TargetMaxNsPerSpinIteration / 1.5 + 1;

    return max(MinNsPerYield, min((double)elapsedTicks * NsPerS / ((double)yieldCount * ticksPerS), MaxNsPerYield));
}

#if defined(HOST_ARM64) && !defined(_MSC_VER)
// Measures the duration of the raw YIELD instruction (bypassing WFET).
// This is needed for the GC's scaling factor: the GC uses its own YieldProcessor()
// which always issues YIELD, so the GC needs calibration based on raw YIELD timing
// even when System_YieldProcessor() uses WFET.
static double MeasureRawNsPerYield(unsigned int measureDurationUs)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
#ifndef FEATURE_NATIVEAOT
        MODE_PREEMPTIVE;
#endif
    }
    CONTRACTL_END;

    int yieldCount = (int)(measureDurationUs * 1000 / s_rawYieldNsPerYield) + 1;
    int64_t ticksPerS = s_performanceCounterTicksPerS;
    int64_t measureDurationTicks = ticksPerS * measureDurationUs / (1000 * 1000);

    int64_t startTicks = minipal_hires_ticks();

    for (int i = 0; i < yieldCount; ++i)
    {
#ifdef FEATURE_NATIVEAOT
        PalYieldProcessor();
#else
        YieldProcessor();
#endif
    }

    int64_t elapsedTicks = minipal_hires_ticks() - startTicks;
    while (elapsedTicks < measureDurationTicks)
    {
        int nextYieldCount =
            max(4,
                elapsedTicks == 0
                    ? yieldCount / 4
                    : (int)(yieldCount * (measureDurationTicks - elapsedTicks) / (double)elapsedTicks) + 1);
        for (int i = 0; i < nextYieldCount; ++i)
        {
#ifdef FEATURE_NATIVEAOT
            PalYieldProcessor();
#else
            YieldProcessor();
#endif
        }

        elapsedTicks = minipal_hires_ticks() - startTicks;
        yieldCount += nextYieldCount;
    }

    const double MinNsPerYield = 0.1;
    const double MaxNsPerYield = YieldProcessorNormalization::TargetMaxNsPerSpinIteration / 1.5 + 1;

    return max(MinNsPerYield, min((double)elapsedTicks * NsPerS / ((double)yieldCount * ticksPerS), MaxNsPerYield));
}
#endif // HOST_ARM64 && !_MSC_VER

void YieldProcessorNormalization::PerformMeasurement()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
#ifndef FEATURE_NATIVEAOT
        MODE_PREEMPTIVE;
#endif
    }
    CONTRACTL_END;

    _ASSERTE(s_isMeasurementScheduled);

    double latestNsPerYield;
    if (s_normalizationState == NormalizationState::Initialized)
    {
        if ((minipal_lowres_ticks() - s_previousNormalizationTimeMs) < MeasurementPeriodMs)
        {
            return;
        }

        int nextMeasurementIndex = s_nextMeasurementIndex;
        latestNsPerYield = MeasureNsPerYield(DetermineMeasureDurationUs());
        AtomicStore(&s_nsPerYieldMeasurements[nextMeasurementIndex], latestNsPerYield);
        if (++nextMeasurementIndex >= NsPerYieldMeasurementCount)
        {
            nextMeasurementIndex = 0;
        }
        s_nextMeasurementIndex = nextMeasurementIndex;
    }
    else if (s_normalizationState == NormalizationState::Uninitialized)
    {
#ifdef FEATURE_NATIVEAOT
        if ((s_performanceCounterTicksPerS = minipal_hires_tick_frequency()) < 1000 * 1000)
#else
        int64_t freq = minipal_hires_tick_frequency();
        if (freq < 1000 * 1000)
#endif
        {
            // High precision clock not available or clock resolution is too low, resort to defaults
            s_normalizationState = NormalizationState::Failed;
            return;
        }

#ifndef FEATURE_NATIVEAOT
        s_performanceCounterTicksPerS = freq;
#endif

#if defined(HOST_ARM64) && !defined(_MSC_VER)
        // Configure ARM64 yield type for spin-wait.
        // Read DOTNET_Arm64YieldType: 0=auto, 1=yield, 2=wfet, 3=isb
        // Read DOTNET_Arm64WfetDelayNs: WFET delay in nanoseconds (default 37, range 1-1000)
        // These use getenv() directly so both CoreCLR and NativeAOT can read them.
        {
            Arm64YieldType requestedType = Arm64YieldType::Auto;
            const char* yieldTypeEnv = getenv("DOTNET_Arm64YieldType");
            if (yieldTypeEnv != nullptr)
            {
                int val = atoi(yieldTypeEnv);
                if (val >= 0 && val <= 2)
                {
                    requestedType = (Arm64YieldType)val;
                }
            }

            uint32_t wfetDelayNs = YieldProcessorNormalization::TargetNsPerNormalizedYield;
            const char* delayEnv = getenv("DOTNET_Arm64WfetDelayNs");
            if (delayEnv != nullptr)
            {
                int val = atoi(delayEnv);
                if (val >= 1 && val <= 1000)
                {
                    wfetDelayNs = (uint32_t)val;
                }
            }

            int cpuFeatures = minipal_getcpufeatures();
            bool wfetHwAvailable = ((cpuFeatures & ARM64IntrinsicConstants_WFxT) != 0) &&
                                   ((cpuFeatures & ARM64IntrinsicConstants_Ecv) != 0);

            if (wfetHwAvailable)
            {
                // Validate that the counter frequency is at least 1GHz (mandated by ARMv8.6+).
                uint64_t cntfrq;
                __asm__ __volatile__("mrs %0, cntfrq_el0" : "=r" (cntfrq));
                if (cntfrq < 1000000000ULL)
                {
                    wfetHwAvailable = false;
                }
            }

            bool enableWfet = false;
            switch (requestedType)
            {
                case Arm64YieldType::Auto:
                    enableWfet = wfetHwAvailable;
                    break;
                case Arm64YieldType::Yield:
                    enableWfet = false;
                    break;
                case Arm64YieldType::Wfet:
                    enableWfet = wfetHwAvailable;
                    break;
            }

            if (enableWfet)
            {
                // Write delay first, then publish the flag (memory ordering on ARM64 weak model).
                g_arm64WfetDelayNs = wfetDelayNs;
                VolatileStore(&g_arm64UseWfet, true);
            }
        }
#endif // HOST_ARM64 && !_MSC_VER

        unsigned int measureDurationUs = DetermineMeasureDurationUs();
        for (int i = 0; i < NsPerYieldMeasurementCount; ++i)
        {
            latestNsPerYield = MeasureNsPerYield(measureDurationUs);
            AtomicStore(&s_nsPerYieldMeasurements[i], latestNsPerYield);
            if (i == 0 || latestNsPerYield < s_establishedNsPerYield)
            {
                AtomicStore(&s_establishedNsPerYield, latestNsPerYield);
            }
            if (i < NsPerYieldMeasurementCount - 1)
            {
#ifdef FEATURE_EVENT_TRACE
                FireEtwYieldProcessorMeasurement(GetClrInstanceId(), latestNsPerYield, s_establishedNsPerYield);
#endif //FEATURE_EVENT_TRACE
            }
        }
    }
    else
    {
        _ASSERTE(s_normalizationState == NormalizationState::Failed);
        return;
    }

    double establishedNsPerYield = s_nsPerYieldMeasurements[0];
    for (int i = 1; i < NsPerYieldMeasurementCount; ++i)
    {
        double nsPerYield = s_nsPerYieldMeasurements[i];
        if (nsPerYield < establishedNsPerYield)
        {
            establishedNsPerYield = nsPerYield;
        }
    }
    if (establishedNsPerYield != s_establishedNsPerYield)
    {
        AtomicStore(&s_establishedNsPerYield, establishedNsPerYield);
    }
#ifdef FEATURE_EVENT_TRACE
    FireEtwYieldProcessorMeasurement(GetClrInstanceId(), latestNsPerYield, s_establishedNsPerYield);
#endif //FEATURE_EVENT_TRACE
    // Calculate the number of yields required to span the duration of a normalized yield
    unsigned int yieldsPerNormalizedYield = max(1u, (unsigned int)(TargetNsPerNormalizedYield / establishedNsPerYield + 0.5));
    _ASSERTE(yieldsPerNormalizedYield <= MaxYieldsPerNormalizedYield);
    s_yieldsPerNormalizedYield = yieldsPerNormalizedYield;

    // Calculate the maximum number of yields that would be optimal for a late spin iteration. Typically, we would not want to
    // spend excessive amounts of time (thousands of cycles) doing only YieldProcessor, as SwitchToThread/Sleep would do a
    // better job of allowing other work to run.
    s_optimalMaxNormalizedYieldsPerSpinIteration =
        max(1u, (unsigned int)(TargetMaxNsPerSpinIteration / (yieldsPerNormalizedYield * establishedNsPerYield) + 0.5));
    _ASSERTE(s_optimalMaxNormalizedYieldsPerSpinIteration <= MaxOptimalMaxNormalizedYieldsPerSpinIteration);

#if defined(HOST_ARM64) && !defined(_MSC_VER)
    if (g_arm64UseWfet)
    {
        // When WFET is active, the GC still uses raw YIELD (not WFET) in its own
        // YieldProcessor() calls. We must pass a scaling factor based on raw YIELD
        // timing to prevent the GC's spin counts from collapsing.
        unsigned int measureDurationUsForGc = DetermineMeasureDurationUs();
        double rawNsPerYield = MeasureRawNsPerYield(measureDurationUsForGc);
        s_rawYieldNsPerYield = rawNsPerYield;
        unsigned int rawYieldsPerNormalizedYield = max(1u, (unsigned int)(TargetNsPerNormalizedYield / rawNsPerYield + 0.5));
        GCHeapUtilities::GetGCHeap()->SetYieldProcessorScalingFactor((float)rawYieldsPerNormalizedYield);
    }
    else
#endif // HOST_ARM64 && !_MSC_VER
    {
        GCHeapUtilities::GetGCHeap()->SetYieldProcessorScalingFactor((float)yieldsPerNormalizedYield);
    }

    s_previousNormalizationTimeMs = minipal_lowres_ticks();
    s_normalizationState = NormalizationState::Initialized;
    s_isMeasurementScheduled = false;
}


void YieldProcessorNormalization::ScheduleMeasurementIfNecessary()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

    NormalizationState normalizationState = VolatileLoadWithoutBarrier(&s_normalizationState);
    if (normalizationState == NormalizationState::Initialized)
    {
        if ((minipal_lowres_ticks() - s_previousNormalizationTimeMs) < MeasurementPeriodMs)
        {
            return;
        }
    }
    else if (normalizationState == NormalizationState::Uninitialized)
    {
    }
    else
    {
        _ASSERTE(normalizationState == NormalizationState::Failed);
        return;
    }

#ifdef FEATURE_NATIVEAOT
    if (s_isMeasurementScheduled)
#else
    // !g_fEEStarted is required for FinalizerThread::EnableFinalization() below
    if (s_isMeasurementScheduled || !g_fEEStarted)
#endif
    {
        return;
    }

    s_isMeasurementScheduled = true;
#ifdef FEATURE_NATIVEAOT
    RhEnableFinalization();
#else
    FinalizerThread::EnableFinalization();
#endif
}

void YieldProcessorNormalization::FireMeasurementEvents()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;

#ifdef FEATURE_EVENT_TRACE
    if (!EventEnabledYieldProcessorMeasurement())
    {
        return;
    }

    // This function may be called at any time to fire events about recorded measurements. There is no synchronization for the
    // recorded information, so try to enumerate the array with some care.
    double establishedNsPerYield = AtomicLoad(&s_establishedNsPerYield);
    int nextIndex = VolatileLoadWithoutBarrier(&s_nextMeasurementIndex);
    for (int i = 0; i < NsPerYieldMeasurementCount; ++i)
    {
        double nsPerYield = AtomicLoad(&s_nsPerYieldMeasurements[nextIndex]);
        if (nsPerYield != 0) // the array may not be fully initialized yet
        {
            FireEtwYieldProcessorMeasurement(GetClrInstanceId(), nsPerYield, establishedNsPerYield);
        }

        if (++nextIndex >= NsPerYieldMeasurementCount)
        {
            nextIndex = 0;
        }
    }
#endif // FEATURE_EVENT_TRACE
}

double YieldProcessorNormalization::AtomicLoad(double *valueRef)
{
    WRAPPER_NO_CONTRACT;

#ifdef TARGET_64BIT
    return VolatileLoadWithoutBarrier(valueRef);
#else
#ifdef FEATURE_NATIVEAOT
    static_assert(sizeof(int64_t) == sizeof(double), "");
    int64_t intRes = PalInterlockedCompareExchange64((int64_t*)valueRef, 0, 0);
    return *(double*)(int64_t*)(&intRes);
#else
    return InterlockedCompareExchangeT(valueRef, 0.0, 0.0);
#endif
#endif
}

void YieldProcessorNormalization::AtomicStore(double *valueRef, double value)
{
    WRAPPER_NO_CONTRACT;

#ifdef TARGET_64BIT
    *valueRef = value;
#else
#ifdef FEATURE_NATIVEAOT
    static_assert(sizeof(int64_t) == sizeof(double), "");
    PalInterlockedExchange64((int64_t *)valueRef, *(int64_t *)(double*)&value);
#else
    InterlockedExchangeT(valueRef, value);
#endif
#endif
}

