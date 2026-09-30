// swift-tools-version: 6.2
import PackageDescription

// Portable usage engine for CodexBar on Windows. The WPF tray application under Windows/
// launches CodexBarWindowsEngine as a short-lived child process and reads its JSON output.
// Keep these targets Foundation-only so they build on Windows without AppKit, POSIX,
// browser-cookie, or Keychain dependencies.
let strictConcurrency: [SwiftSetting] = [
    .enableUpcomingFeature("StrictConcurrency"),
]

let package = Package(
    name: "CodexBar",
    platforms: [
        // Lets contributors run the portable tests on a Mac as well.
        .macOS(.v14),
    ],
    products: [
        .library(name: "CodexBarPortableCore", targets: ["CodexBarPortableCore"]),
        .executable(name: "CodexBarWindowsEngine", targets: ["CodexBarWindowsEngine"]),
    ],
    targets: [
        .target(
            name: "CodexBarPortableCore",
            path: "Sources/CodexBarPortableCore",
            swiftSettings: strictConcurrency),
        .executableTarget(
            name: "CodexBarWindowsEngine",
            dependencies: ["CodexBarPortableCore"],
            path: "Sources/CodexBarWindowsEngine",
            swiftSettings: strictConcurrency),
        .testTarget(
            name: "CodexBarPortableCoreTests",
            dependencies: ["CodexBarPortableCore"],
            path: "Tests/CodexBarPortableCoreTests",
            swiftSettings: strictConcurrency + [
                .enableExperimentalFeature("SwiftTesting"),
            ]),
    ])
