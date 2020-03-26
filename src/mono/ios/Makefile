MONO_CONFIG=Debug

# change to x64 for simulator
MONO_ARCH=x64
MIN_IOS=10.1
ARTIFACTS_BIN=../../../artifacts/bin/
ARTIFACTS_BCL=$(ARTIFACTS_BIN)runtime/netcoreapp5.0-iOS-$(MONO_CONFIG)-$(MONO_ARCH)
ARTIFACTS_MONO=$(ARTIFACTS_BIN)/mono/iOS.$(MONO_ARCH).$(MONO_CONFIG)

DOTNET := $(shell cd ../netcore && bash init-tools.sh | tail -1)
SYSROOT := $(shell xcrun --sdk iphoneos --show-sdk-path)

# copy all BCL libs and Mono runtime to `bcl-*` folder
prepare: bcl-$(MONO_ARCH)/.configured

bcl-$(MONO_ARCH)/.configured:
	rm -rf bcl-$(MONO_ARCH)
	mkdir bcl-$(MONO_ARCH)
	cp $(ARTIFACTS_BCL)/*.dll bcl-$(MONO_ARCH)/
	cp $(ARTIFACTS_MONO)/*.{dll,dylib,pdb} bcl-$(MONO_ARCH)/
	for lib in bcl-$(MONO_ARCH)/*.dll; do make aot-lib-${MONO_ARCH} LIB=$$lib; done
	touch bcl-$(MONO_ARCH)/.configured

aot-bcl-%:
	make aot-lib-$(MONO_ARCH) LIB=bcl-$(MONO_ARCH)/$*

# run AOT for all libs inside bcl-*
aot-bcl: $(addprefix aot-bcl-, $(notdir $(wildcard bcl-$(MONO_ARCH)/*.dll)))

aot-test:
	@if test -z "$(TEST_DIR)"; then echo "TEST_DIR is not set"; exit 1; fi
	if [ ! -d "$(ARTIFACTS_BIN)$(TEST_DIR)" ]; then echo "$(ARTIFACTS_BIN)$(TEST_DIR) doesn't exist"; exit 1; fi
	$(DOTNET) build -c Debug TestRunner.csproj
	cp bin/TestRunner/TestRunner.dll $(ARTIFACTS_BIN)$(TEST_DIR)/
	make -j$(NPROCS) prepare
		for assembly in $(ARTIFACTS_BIN)$(TEST_DIR)/*.dll; do \
		make aot-lib-$(MONO_ARCH) LIB=$$assembly; \
	done;
	make xcode MONO_TEST=$(ARTIFACTS_BIN)$(TEST_DIR)

# no-op: we'll use regular jit for simulator
aot-lib-x64:

# run AOT compilation for $(LIB)
aot-lib-arm64:
	echo "[AOT] $(LIB)";
	DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 MONO_PATH=bcl-$(MONO_ARCH) \
	$(ARTIFACTS_MONO)/cross/./mono-aot-cross -O=gsharedvt,float32 --nollvm --debug \
	--aot=mtriple=arm64-ios,static,asmonly,direct-icalls,no-direct-calls,dwarfdebug,full, $(LIB) && \
	clang -isysroot $(SYSROOT) -miphoneos-version-min=$(MIN_IOS) -arch arm64 -c $(LIB).s

# rebuild mono, bcl and tests for iOS-$(MONO_ARCH)
mono-libraries-tests:
	 ../../.././build.sh -os iOS -arch $(MONO_ARCH) --subsetCategory mono-libraries /p:DisableCrossgen=true
	 ../../.././build.sh -os iOS -arch $(MONO_ARCH) --buildtests --subsetCategory mono-libraries /p:DisableCrossgen=true

# generate an xcode project
xcode:
	cmake -S. -BXcode -GXcode \
	-DCMAKE_SYSTEM_NAME=iOS \
	"-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64" \
	-DCMAKE_OSX_DEPLOYMENT_TARGET=$(MIN_IOS) \
	-DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
	-DMONO_CONFIG=$(MONO_CONFIG) \
	-DMONO_ARCH=$(MONO_ARCH) \
	-DMONO_TEST=$(MONO_TEST) \
	-DMONO_BCL=bcl-$(MONO_ARCH)

# build *.app and deploy to a simulator
IOS_DEPLOY_SDK=iphonesimulator13.2
IOS_DEVICE="iPhone 11"
run-tests-sim:
	xcodebuild -project Xcode/XunitTestRunner.xcodeproj -arch x86_64 -sdk $(IOS_DEPLOY_SDK)
	xcrun simctl shutdown $(IOS_DEVICE) || true
	xcrun simctl boot $(IOS_DEVICE)
	open -a Simulator
	xcrun simctl install $(IOS_DEVICE) Xcode/Debug-iphonesimulator/XunitTestRunner.app
	xcrun simctl launch booted net.dot.XunitTestRunner

clean:
	rm -rf bcl-*
	rm -rf bin
	rm -rf Xcode
