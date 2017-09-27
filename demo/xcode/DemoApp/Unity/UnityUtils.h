#import <Foundation/Foundation.h>

#ifndef UnityUtils_h
#define UnityUtils_h

void unity_init(int argc, char* argv[]);

void UnityPostMessage(NSString* gameObject, NSString* methodName, NSString* message);

#endif /* UnityUtils_h */
