import Foundation
import Testing
@testable import CodexBarPortableCore

struct PortableCredentialsTests {
    @Test
    func `Codex snake case credentials decode`() throws {
        let data = Data("""
        {
          "tokens": {
            "access_token": "access",
            "refresh_token": "refresh",
            "id_token": "id",
            "account_id": "account"
          },
          "last_refresh": "2026-07-27T12:00:00Z"
        }
        """.utf8)

        let credentials = try PortableCredentialLoader.parseCodex(data)

        #expect(credentials.accessToken == "access")
        #expect(credentials.refreshToken == "refresh")
        #expect(credentials.idToken == "id")
        #expect(credentials.accountID == "account")
        #expect(credentials.lastRefresh != nil)
    }

    @Test
    func `Codex camel case credentials decode`() throws {
        let data = Data("""
        {
          "tokens": {
            "accessToken": "access",
            "refreshToken": "refresh",
            "accountId": "account"
          }
        }
        """.utf8)

        let credentials = try PortableCredentialLoader.parseCodex(data)

        #expect(credentials.accessToken == "access")
        #expect(credentials.refreshToken == "refresh")
        #expect(credentials.accountID == "account")
    }

    @Test
    func `Claude Code credentials decode milliseconds`() throws {
        let data = Data("""
        {
          "claudeAiOauth": {
            "accessToken": "claude-access",
            "refreshToken": "claude-refresh",
            "expiresAt": 1785157200000,
            "rateLimitTier": "default_claude_max_20x",
            "subscriptionType": "max"
          }
        }
        """.utf8)

        let credentials = try PortableCredentialLoader.parseClaude(data)

        #expect(credentials.accessToken == "claude-access")
        #expect(credentials.refreshToken == "claude-refresh")
        #expect(credentials.expiresAt == Date(timeIntervalSince1970: 1_785_157_200))
        #expect(credentials.subscriptionType == "max")
    }

    @Test
    func `Claude Code refreshed credentials replace the access token atomically`() throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let environment = PortableHostEnvironment(values: ["HOME": root.path])
        try FileManager.default.createDirectory(
            at: environment.claudeConfigDirectory,
            withIntermediateDirectories: true)
        try Data("""
        {
          "claudeAiOauth": {
            "accessToken": "old-access",
            "refreshToken": "old-refresh",
            "expiresAt": 1000,
            "subscriptionType": "pro"
          },
          "unrelatedSetting": true
        }
        """.utf8).write(to: environment.claudeCredentialsURL)

        let refreshed = PortableClaudeCredentials(
            accessToken: "new-access",
            refreshToken: "new-refresh",
            expiresAt: Date(timeIntervalSince1970: 1_785_157_200),
            rateLimitTier: nil,
            subscriptionType: "pro")
        try PortableCredentialLoader.saveClaude(
            refreshed,
            environment: environment,
            reader: PortableCredentialFileReader(),
            writer: PortableCredentialFileWriter())

        let saved = try PortableCredentialLoader.loadClaude(environment: environment)
        #expect(saved.accessToken == "new-access")
        #expect(saved.refreshToken == "new-refresh")
        #expect(saved.expiresAt == Date(timeIntervalSince1970: 1_785_157_200))
    }
}
