// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

// Enable calling ICU functions through shims to enable support for
// multiple versions of ICU.

#pragma once

#if defined(TARGET_UNIX)

#include "config.h"

#if defined(TARGET_ANDROID)
#include "pal_icushim_internal_android.h"
#else

#define U_DISABLE_RENAMING 1

// All ICU headers need to be included here so that all function prototypes are
// available before the function pointers are declared below.
#include <unicode/ucurr.h>
#include <unicode/ucal.h>
#include <unicode/uchar.h>
#include <unicode/ucol.h>
#include <unicode/udat.h>
#include <unicode/udatpg.h>
#include <unicode/uenum.h>
#include <unicode/uidna.h>
#include <unicode/uldnames.h>
#include <unicode/ulocdata.h>
#include <unicode/unorm2.h>
#include <unicode/unum.h>
#include <unicode/ures.h>
#include <unicode/usearch.h>
#include <unicode/utf16.h>
#include <unicode/utypes.h>
#include <unicode/urename.h>
#include <unicode/ustring.h>

#endif
#endif
