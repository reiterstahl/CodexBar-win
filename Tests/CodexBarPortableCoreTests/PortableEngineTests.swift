import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif
import Testing
@testable import CodexBarPortableCore

struct PortableEngineTests {
    @Test
    func `Engine returns independent Codex and Claude snapshots without secrets`() async throws {
        let now = Date(timeIntervalSince1970: 1_785_000_000)
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])
        let reader = MemoryCredentialReader(values: [
            environment.codexCredentialsURL.path: Data("""
            {
              "tokens": {
                "access_token": "codex-access",
                "refresh_token": "codex-refresh"
              }
            }
            """.utf8),
            environment.claudeCredentialsURL.path: Data("""
            {
              "claudeAiOauth": {
                "accessToken": "claude-access",
                "refreshToken": "claude-refresh",
                "expiresAt": 2000000000000
              }
            }
            """.utf8),
        ])
        let transport = ProviderFixtureTransport(
            codex: Data("""
            {
              "rate_limit": {
                "primary_window": {
                  "used_percent": 12,
                  "reset_at": 1785160800,
                  "limit_window_seconds": 18000
                }
              }
            }
            """.utf8),
            claude: Data("""
            {
              "five_hour": {
                "utilization": 21,
                "resets_at": "2026-07-27T18:00:00Z"
              }
            }
            """.utf8))
        let engine = CodexBarPortableEngine(
            environment: environment,
            credentialReader: reader,
            transport: transport,
            now: { now })

        let snapshot = await engine.snapshot()

        #expect(snapshot.providers.map(\.provider) == [.codex, .claude])
        #expect(snapshot.failures.isEmpty)
        #expect(snapshot.generatedAt == now)
        let encoded = try JSONEncoder().encode(snapshot)
        let output = try #require(String(data: encoded, encoding: .utf8))
        #expect(!output.contains("codex-access"))
        #expect(!output.contains("codex-refresh"))
        #expect(!output.contains("claude-access"))
        #expect(!output.contains("claude-refresh"))
    }

    @Test
    func `Codex snapshot includes reset credits and survives their failure`() async throws {
        let now = Date(timeIntervalSince1970: 1_790_000_000)
        let credentials = PortableCodexCredentials(
            accessToken: "access",
            refreshToken: "refresh",
            idToken: nil,
            accountID: "account",
            lastRefresh: nil)
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])
        let usage = Data("""
        {"rate_limit": {"primary_window": {"used_percent": 40, "reset_at": 1790010000, "limit_window_seconds": 18000}}}
        """.utf8)

        let withCredits = try await PortableCodexProvider.fetch(
            credentials: credentials,
            environment: environment,
            transport: CodexResetCreditsTransport(usage: usage, credits: Data("""
            {"available_count": 1, "credits": [{"status": "available", "expires_at": "2026-10-20T00:00:00Z"}]}
            """.utf8), creditsStatus: 200),
            now: now)
        #expect(withCredits.resetCredits?.availableCount == 1)

        let withoutCredits = try await PortableCodexProvider.fetch(
            credentials: credentials,
            environment: environment,
            transport: CodexResetCreditsTransport(usage: usage, credits: Data(), creditsStatus: 500),
            now: now)
        #expect(withoutCredits.resetCredits == nil)
        #expect(withoutCredits.windows.count == 1)
    }

    @Test
    func `Engine keeps Claude result when Codex credentials fail`() async {
        let now = Date(timeIntervalSince1970: 1_785_000_000)
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])
        let reader = MemoryCredentialReader(values: [
            environment.claudeCredentialsURL.path: Data("""
            {
              "claudeAiOauth": {
                "accessToken": "claude-access",
                "expiresAt": 2000000000000
              }
            }
            """.utf8),
        ])
        let transport = ProviderFixtureTransport(
            codex: Data(),
            claude: Data("""
            {
              "five_hour": {
                "utilization": 21,
                "resets_at": "2026-07-27T18:00:00Z"
              }
            }
            """.utf8))
        let engine = CodexBarPortableEngine(
            environment: environment,
            credentialReader: reader,
            transport: transport,
            now: { now })

        let snapshot = await engine.snapshot()

        #expect(snapshot.providers.map(\.provider) == [.claude])
        #expect(snapshot.failures.count == 1)
        #expect(snapshot.failures.first?.provider == .codex)
        #expect(snapshot.failures.first?.code == "credentials_unreadable")
    }

    @Test
    func `Engine refreshes an expired Claude token and persists its rotation`() async throws {
        let now = Date(timeIntervalSince1970: 1_785_000_000)
        let environment = PortableHostEnvironment(values: ["HOME": "/users/tester"])
        let store = MutableCredentialStore(values: [
            environment.claudeCredentialsURL.path: Data("""
            {
              "claudeAiOauth": {
                "accessToken": "expired-access",
                "refreshToken": "old-refresh",
                "expiresAt": 1000
              }
            }
            """.utf8),
        ])
        let engine = CodexBarPortableEngine(
            environment: environment,
            credentialReader: store,
            credentialWriter: store,
            transport: RefreshingClaudeFixtureTransport(),
            now: { now })

        let snapshot = await engine.snapshot(providers: [.claude])

        #expect(snapshot.providers.map(\.provider) == [.claude])
        #expect(snapshot.failures.isEmpty)
        let persisted = try PortableCredentialLoader.parseClaude(
            try #require(store.value(at: environment.claudeCredentialsURL)))
        #expect(persisted.accessToken == "refreshed-access")
        #expect(persisted.refreshToken == "refreshed-refresh")
    }
}

private struct CodexResetCreditsTransport: PortableHTTPTransport {
    let usage: Data
    let credits: Data
    let creditsStatus: Int

    func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        if request.url?.path.hasSuffix("/rate-limit-reset-credits") == true {
            #expect(request.value(forHTTPHeaderField: "ChatGPT-Account-ID") == "account")
            return PortableHTTPResponse(data: self.credits, statusCode: self.creditsStatus)
        }
        return PortableHTTPResponse(data: self.usage, statusCode: 200)
    }
}

private struct MemoryCredentialReader: PortableCredentialFileReading {
    let values: [String: Data]

    func data(at url: URL) throws -> Data {
        guard let data = self.values[url.path] else {
            throw MemoryCredentialReaderError.notFound
        }
        return data
    }
}

private enum MemoryCredentialReaderError: Error {
    case notFound
}

private final class MutableCredentialStore: PortableCredentialFileReading, PortableCredentialFileWriting, @unchecked Sendable {
    private let lock = NSLock()
    private var values: [String: Data]

    init(values: [String: Data]) {
        self.values = values
    }

    func data(at url: URL) throws -> Data {
        guard let value = self.value(at: url) else {
            throw MemoryCredentialReaderError.notFound
        }
        return value
    }

    func replace(_ data: Data, at url: URL) throws {
        self.lock.withLock {
            self.values[url.path] = data
        }
    }

    func value(at url: URL) -> Data? {
        self.lock.withLock { self.values[url.path] }
    }
}

private struct ProviderFixtureTransport: PortableHTTPTransport {
    let codex: Data
    let claude: Data

    func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        if request.url?.host == "api.anthropic.com" {
            return PortableHTTPResponse(data: self.claude, statusCode: 200)
        }
        return PortableHTTPResponse(data: self.codex, statusCode: 200)
    }
}

private struct RefreshingClaudeFixtureTransport: PortableHTTPTransport {
    func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        if request.url?.host == "platform.claude.com" {
            return PortableHTTPResponse(data: Data("""
            {
              "access_token": "refreshed-access",
              "refresh_token": "refreshed-refresh",
              "expires_in": 28800
            }
            """.utf8), statusCode: 200)
        }
        return PortableHTTPResponse(data: Data("""
        {
          "five_hour": {
            "utilization": 21,
            "resets_at": "2026-07-27T18:00:00Z"
          }
        }
        """.utf8), statusCode: 200)
    }
}
