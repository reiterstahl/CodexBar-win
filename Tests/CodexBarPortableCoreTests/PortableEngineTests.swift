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
