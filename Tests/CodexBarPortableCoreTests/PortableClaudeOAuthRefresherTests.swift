import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif
import Testing
@testable import CodexBarPortableCore

struct PortableClaudeOAuthRefresherTests {
    @Test
    func `expired Claude access token refreshes without a browser login`() async throws {
        let now = Date(timeIntervalSince1970: 1_785_000_000)
        let credentials = PortableClaudeCredentials(
            accessToken: "expired-access",
            refreshToken: "stored-refresh",
            expiresAt: now.addingTimeInterval(-1),
            rateLimitTier: "default",
            subscriptionType: "pro")

        let refreshed = try await PortableClaudeOAuthRefresher.refresh(
            credentials,
            transport: RefreshFixtureTransport(),
            now: now)

        #expect(refreshed.accessToken == "renewed-access")
        #expect(refreshed.refreshToken == "rotated-refresh")
        #expect(refreshed.expiresAt == now.addingTimeInterval(28_800))
        #expect(refreshed.subscriptionType == "pro")
    }
}

private struct RefreshFixtureTransport: PortableHTTPTransport {
    func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        #expect(request.url?.absoluteString == "https://platform.claude.com/v1/oauth/token")
        #expect(request.httpMethod == "POST")
        let body = try #require(request.httpBody.flatMap { String(data: $0, encoding: .utf8) })
        #expect(body.contains("grant_type=refresh_token"))
        #expect(body.contains("refresh_token=stored-refresh"))
        return PortableHTTPResponse(data: Data("""
        {
          "access_token": "renewed-access",
          "refresh_token": "rotated-refresh",
          "expires_in": 28800
        }
        """.utf8), statusCode: 200)
    }
}
