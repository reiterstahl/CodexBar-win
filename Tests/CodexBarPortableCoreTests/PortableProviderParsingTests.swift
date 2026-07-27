import Foundation
import Testing
@testable import CodexBarPortableCore

struct PortableProviderParsingTests {
    @Test
    func `Codex response maps session and weekly windows`() throws {
        let credentials = PortableCodexCredentials(
            accessToken: "access",
            refreshToken: "refresh",
            idToken: nil,
            accountID: "account",
            lastRefresh: nil)
        let data = Data("""
        {
          "plan_type": "plus",
          "rate_limit": {
            "primary_window": {
              "used_percent": 25,
              "reset_at": 1785160800,
              "limit_window_seconds": 18000
            },
            "secondary_window": {
              "used_percent": 40,
              "reset_at": 1785679200,
              "limit_window_seconds": 604800
            }
          }
        }
        """.utf8)

        let snapshot = try PortableCodexProvider.parse(
            data,
            credentials: credentials,
            now: Date(timeIntervalSince1970: 100))

        #expect(snapshot.provider == .codex)
        #expect(snapshot.windows.count == 2)
        #expect(snapshot.windows[0].id == "session")
        #expect(snapshot.windows[0].remainingPercent == 75)
        #expect(snapshot.windows[1].windowMinutes == 10080)
        #expect(snapshot.identity?.plan == "plus")
    }

    @Test
    func `Claude response maps standard and scoped windows`() throws {
        let credentials = PortableClaudeCredentials(
            accessToken: "access",
            refreshToken: "refresh",
            expiresAt: Date(timeIntervalSince1970: 2_000_000_000),
            rateLimitTier: "tier",
            subscriptionType: "max")
        let data = Data("""
        {
          "five_hour": {
            "utilization": 10,
            "resets_at": "2026-07-27T18:00:00Z"
          },
          "seven_day": {
            "utilization": 35,
            "resets_at": "2026-08-02T18:00:00Z"
          },
          "limits": [
            {
              "kind": "weekly_scoped",
              "group": "weekly",
              "percent": 22,
              "resets_at": "2026-08-02T18:00:00Z",
              "scope": {
                "model": {
                  "id": "model-a",
                  "display_name": "Model A"
                }
              }
            }
          ]
        }
        """.utf8)

        let snapshot = try PortableClaudeProvider.parse(
            data,
            credentials: credentials,
            now: Date(timeIntervalSince1970: 100))

        #expect(snapshot.provider == .claude)
        #expect(snapshot.windows.map(\.id) == ["session", "weekly", "weekly-model-a"])
        #expect(snapshot.identity?.plan == "max")
    }

    @Test
    func `Provider percentages are clamped`() {
        let low = PortableRateWindow(
            id: "low",
            label: "Low",
            usedPercent: -10,
            windowMinutes: nil,
            resetsAt: nil)
        let high = PortableRateWindow(
            id: "high",
            label: "High",
            usedPercent: 140,
            windowMinutes: nil,
            resetsAt: nil)

        #expect(low.usedPercent == 0)
        #expect(high.usedPercent == 100)
    }

    @Test
    func `Codex custom API base uses Codex usage path`() throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("codexbar-portable-url-\(UUID().uuidString)", isDirectory: true)
        let codexHome = root.appendingPathComponent(".codex", isDirectory: true)
        try FileManager.default.createDirectory(at: codexHome, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        try Data(#"chatgpt_base_url = "https://example.test/v1""#.utf8)
            .write(to: codexHome.appendingPathComponent("config.toml"))
        let environment = PortableHostEnvironment(values: [
            "HOME": root.path,
            "CODEX_HOME": codexHome.path,
        ])

        #expect(
            PortableCodexProvider.usageURL(environment: environment).absoluteString
                == "https://example.test/v1/api/codex/usage")
    }
}
