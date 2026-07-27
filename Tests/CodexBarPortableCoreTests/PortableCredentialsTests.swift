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
}
