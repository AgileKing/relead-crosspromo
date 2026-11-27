// Relead Cross-Promo: tiny iOS bridge. canOpenURL for the installed check, identifierForVendor for attribution.
#import <UIKit/UIKit.h>

static char* ReleadCP_Copy(NSString* s)
{
    if (s == nil) return NULL;
    const char* utf8 = [s UTF8String];
    char* out = (char*)malloc(strlen(utf8) + 1);
    strcpy(out, utf8);
    return out; // freed by the Mono/IL2CPP marshaller
}

extern "C" {

bool _ReleadCP_CanOpenUrl(const char* url)
{
    if (url == NULL) return false;
    NSURL* u = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
    if (u == nil) return false;
    return [[UIApplication sharedApplication] canOpenURL:u];
}

char* _ReleadCP_VendorId()
{
    NSUUID* v = [[UIDevice currentDevice] identifierForVendor];
    return v == nil ? NULL : ReleadCP_Copy([v UUIDString]);
}

}
