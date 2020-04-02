MONO_CONFIG=Debug

# change to x64 for simulator
MONO_ARCH=arm64
MIN_IOS=10.1
ARTIFACTS_BIN=../../../artifacts/bin/
ARTIFACTS_BCL=$(ARTIFACTS_BIN)runtime/netcoreapp5.0-iOS-$(MONO_CONFIG)-$(MONO_ARCH)
ARTIFACTS_MONO=$(ARTIFACTS_BIN)/mono/iOS.$(MONO_ARCH).$(MONO_CONFIG)

DOTNET := $(shell cd ../netcore && bash init-tools.sh | tail -1)
SYSROOT := $(shell xcrun --sdk iphoneos --show-sdk-path)

BCL_EXCLUDES =\
	tmp-$(MONO_ARCH)/System.Runtime.WindowsRuntime.dll

# sample: rebuild everything and run xunit tests for
# 'System.Numerics.Vectors.Tests.dll'
# NOTE: don't use tests suites with remote executor usages 
# NOTE2: run 'run-tests-sim' after in order to deploy & run on a simulator
# NOTE3: make sure `mono-libraries-tests` is executed first
run-System.Numerics.Vectors.Tests:
	make aot-test \
	TEST_DIR=System.Numerics.Vectors.Tests/netcoreapp5.0-Debug

# rebuild mono, bcl and tests for iOS-$(MONO_ARCH)
# Ignore Utf8String* build errors (TODO: fix)
mono-libraries-tests:
	 ../../.././build.sh -os iOS -arch $(MONO_ARCH) --subsetCategory mono-libraries /p:DisableCrossgen=true
	 ../../.././build.sh -os iOS -arch $(MONO_ARCH) --buildtests --subsetCategory mono-libraries /p:DisableCrossgen=true

# copy all dlls into a tmp folder (tmp-$(MONO_ARCH)) including the whole BCL
# and run AOT for them in parallel
aot-test: clean
	mkdir tmp-$(MONO_ARCH)
	@if test -z "$(TEST_DIR)"; then echo "TEST_DIR is not set"; exit 1; fi
	if [ ! -d "$(ARTIFACTS_BIN)$(TEST_DIR)" ]; then echo "$(ARTIFACTS_BIN)$(TEST_DIR) doesn't exist"; exit 1; fi
	$(DOTNET) build -c Debug TestRunner.csproj
	cp bin/TestRunner/TestRunner.dll tmp-$(MONO_ARCH)/
	cp $(ARTIFACTS_BCL)/*.{dll,pdb} tmp-$(MONO_ARCH)/
	cp $(ARTIFACTS_MONO)/*.{dll,pdb} tmp-$(MONO_ARCH)/
	cp $(ARTIFACTS_BIN)$(TEST_DIR)/*.{dll,pdb} tmp-$(MONO_ARCH)/
	make -j$(NPROCS) aot-dir
	$(DOTNET) run -p appbuilder tmp-$(MONO_ARCH) modules.m
	make xcode MONO_APPDIR=tmp-$(MONO_ARCH)

# build *.app and deploy to a simulator
# NOTE: remove `open -a Simulator` and add `--auto-exit` for "quite" mode
IOS_SIM_SDK=iphonesimulator13.2
IOS_SIM_DEVICE="iPhone 11"
run-tests-sim:
	xcodebuild -project Xcode/XunitTestRunner.xcodeproj -arch x86_64 -sdk $(IOS_SIM_SDK)
	xcrun simctl shutdown $(IOS_SIM_DEVICE) || true
	xcrun simctl boot $(IOS_SIM_DEVICE)
	open -a Simulator
	xcrun simctl install $(IOS_SIM_DEVICE) Xcode/Debug-iphonesimulator/XunitTestRunner.app
	xcrun simctl launch booted net.dot.XunitTestRunner

# build *.app and deploy to a real device via `ios-deploy` tool (`brew install ios-deploy`)
IOS_DEVICE_SDK=iphoneos13.2
run-tests-device:
	xcodebuild -project Xcode/XunitTestRunner.xcodeproj -arch arm64 -sdk $(IOS_DEVICE_SDK) -allowProvisioningUpdates DEVELOPMENT_TEAM=H6BUBW32QD
	ios-deploy -b Xcode/Debug-iphoneos/XunitTestRunner.app -L

clean:
	rm -rf modules.m
	rm -rf tmp-*
	rm -rf bin
	rm -rf Xcode


aot-dir-%:
	make aot-lib-$(MONO_ARCH) LIB=tmp-$(MONO_ARCH)/$*

aot-dir: $(addprefix aot-dir-, $(notdir $(filter-out $(BCL_EXCLUDES), $(wildcard tmp-$(MONO_ARCH)/*.dll))))

# no-op: we'll use regular jit for simulator
aot-lib-x64:

# run AOT compilation for $(LIB)
aot-lib-arm64:
	echo "[AOT] $(LIB)";
	DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 MONO_PATH=tmp-$(MONO_ARCH) \
	$(ARTIFACTS_MONO)/cross/./mono-aot-cross -O=gsharedvt,float32 --nollvm --debug \
	--aot=mtriple=arm64-ios,static,asmonly,direct-icalls,no-direct-calls,dwarfdebug,full, $(LIB) && \
	clang -isysroot $(SYSROOT) -miphoneos-version-min=$(MIN_IOS) -arch arm64 -c $(LIB).s -o $(LIB).o

# generate an xcode project
xcode:
	cmake -S. -BXcode -GXcode \
	-DCMAKE_SYSTEM_NAME=iOS \
	"-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64" \
	-DCMAKE_OSX_DEPLOYMENT_TARGET=$(MIN_IOS) \
	-DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
	-DMONO_CONFIG=$(MONO_CONFIG) \
	-DMONO_ARCH=$(MONO_ARCH) \
	-DMONO_APPDIR=$(MONO_APPDIR)
