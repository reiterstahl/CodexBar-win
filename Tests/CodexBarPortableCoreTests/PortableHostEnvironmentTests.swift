import Foundation
import Testing
@testable import CodexBarPortableCore

struct PortableHostEnvironmentTests {
    @Test
    func `Codex home override wins`() {
        let environment = PortableHostEnvironment(
            values: [
                "HOME": "/users/tester",
                "CODEX_HOME": "/portable/codex",
            ])

        #expect(environment.codexCredentialsURL.path == "/portable/codex/auth.json")
        #expect(environment.codexConfigURL.path == "/portable/codex/config.toml")
    }

    @Test
    func `Claude credentials use the resolved home`() {
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])

        #expect(environment.claudeCredentialsURL.path == "/users/tester/.claude/.credentials.json")
    }

    @Test
    func `Claude config override wins`() {
        let environment = PortableHostEnvironment(values: [
            "HOME": "/users/tester",
            "CLAUDE_CONFIG_DIR": "/portable/claude",
        ])

        #expect(environment.claudeCredentialsURL.path == "/portable/claude/.credentials.json")
    }

    @Test
    func `Windows application data uses local app data when compiled for Windows`() {
        let environment = PortableHostEnvironment(values: [
            "HOME": "/users/tester",
            "LOCALAPPDATA": #"C:\Users\tester\AppData\Local"#,
        ])

        #if os(Windows)
        #expect(environment.applicationDataDirectory.path.contains("AppData"))
        #expect(environment.applicationDataDirectory.lastPathComponent == "CodexBar")
        #else
        #expect(environment.applicationDataDirectory.path == "/users/tester/.config/codexbar")
        #endif
    }
}
