#import <UIKit/UIKit.h>
#import "runtime.h"

@interface ViewController : UIViewController
@end

@interface AppDelegate : UIResponder <UIApplicationDelegate>
@property (strong, nonatomic) UIWindow *window;
@property (strong, nonatomic) ViewController *controller;
@end

@implementation AppDelegate
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)launchOptions {
    self.window = [[UIWindow alloc] initWithFrame:[[UIScreen mainScreen] bounds]];
    self.controller = [[ViewController alloc] initWithNibName:nil bundle:nil];
    self.window.rootViewController = self.controller;
    [self.window makeKeyAndVisible];
    return YES;
}
@end

static int testPassed, testSkipped, testFailed;
UILabel *label;

@implementation ViewController

- (void)viewDidLoad {
    [super viewDidLoad];
    
    label = [[UILabel alloc] initWithFrame:[[UIScreen mainScreen] bounds]];
    label.textColor = [UIColor greenColor];
    label.font = [UIFont boldSystemFontOfSize: 30];
    label.numberOfLines = 3;
    label.textAlignment = NSTextAlignmentCenter;
    [self.view addSubview:label];

    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        mono_ios_runtime_init ();
    });
}

@end

void
mono_sdks_ui_increment_testcase_result (int type)
{
    switch (type) {
        case 0: (testPassed)++; break;
        case 1: (testSkipped)++; break;
        case 2: (testFailed)++; break;
    }
    
    dispatch_async(dispatch_get_main_queue(), ^{
        label.text = [NSString stringWithFormat: @"Passed: %i\nSkipped: %i\nFailed: %i", testPassed, testSkipped, testFailed];
    });
}

int main(int argc, char * argv[]) {
    @autoreleasepool {
        return UIApplicationMain(argc, argv, nil, NSStringFromClass([AppDelegate class]));
    }
}
