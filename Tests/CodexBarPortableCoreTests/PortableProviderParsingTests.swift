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

    @Test
    func `Codex reset credits keep only available unexpired credits, soonest first`() throws {
        let now = Date(timeIntervalSince1970: 1_790_000_000)
        let credits = try #require(PortableCodexProvider.parseResetCredits(Data("""
        {
          "available_count": 3,
          "credits": [
            {"id": "a", "reset_type": "rate_limit", "status": "available",
             "granted_at": "2026-09-01T00:00:00Z", "expires_at": "2026-10-20T00:00:00.000Z", "title": "Reset"},
            {"id": "b", "reset_type": "rate_limit", "status": "available",
             "granted_at": "2026-09-01T00:00:00Z", "expires_at": "2026-10-05T12:00:00Z"},
            {"id": "c", "reset_type": "rate_limit", "status": "redeemed",
             "granted_at": "2026-09-01T00:00:00Z", "expires_at": "2026-10-30T00:00:00Z"},
            {"id": "d", "reset_type": "rate_limit", "status": "available",
             "granted_at": "2026-08-01T00:00:00Z", "expires_at": "2026-09-01T00:00:00Z"},
            {"id": "e", "reset_type": "rate_limit", "status": "available",
             "granted_at": "2026-09-01T00:00:00Z"}
          ]
        }
        """.utf8), now: now))

        #expect(credits.availableCount == 3)
        #expect(credits.credits.count == 3)
        #expect(credits.credits[0].expiresAt == ISO8601DateFormatter().date(from: "2026-10-05T12:00:00Z"))
        #expect(credits.credits[1].title == "Reset")
        #expect(credits.credits[2].expiresAt == nil)
    }

    @Test
    func `Codex reset credits accept count-only payloads and reject unrelated JSON`() {
        let now = Date(timeIntervalSince1970: 1_790_000_000)
        let countOnly = PortableCodexProvider.parseResetCredits(Data(#"{"available_count": 2}"#.utf8), now: now)
        #expect(countOnly?.availableCount == 2)
        #expect(countOnly?.credits.isEmpty == true)
        #expect(PortableCodexProvider.parseResetCredits(Data(#"{"rate_limit": {}}"#.utf8), now: now) == nil)
        #expect(PortableCodexProvider.parseResetCredits(Data("not json".utf8), now: now) == nil)
    }

    @Test
    func `Codex reset credits URL sits beside the usage endpoint`() {
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])
        #expect(PortableCodexProvider.resetCreditsURL(environment: environment)?.absoluteString
            == "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits")
    }
}
