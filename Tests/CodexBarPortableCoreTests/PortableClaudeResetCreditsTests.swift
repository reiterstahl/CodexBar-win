import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif
import Testing
@testable import CodexBarPortableCore

struct PortableClaudeResetCreditsTests {
    private let now = Date(timeIntervalSince1970: 1_800_000_000)
    private let credentials = PortableClaudeCredentials(
        accessToken: "fixture-token", refreshToken: nil, expiresAt: nil,
        rateLimitTier: nil, subscriptionType: "pro")

    @Test
    func `saved resets share Claude snapshot and exclude unavailable grants`() throws {
        let data = self.payload("""
        {"eligible":true,"grants":[
          {"id":"private-redemption-handle","label":"private-label","paused":false,
           "resets_left":2,"resets_total":2,"usable_now":false,"ends_at":"2099-01-01T00:00:00.000Z"},
          {"paused":false,"resets_left":1},
          {"paused":true,"resets_left":1},
          {"paused":false,"resets_left":1,"ends_at":"2020-01-01T00:00:00Z"},
          {"paused":false,"resets_left":1,"starts_at":"2099-01-01T00:00:00Z"},
          {"paused":false,"resets_left":0},
          {"paused":false,"resets_left":1,"ends_at":"invalid"},
          {"resets_left":1},
          {"paused":false,"resets_left":3,"resets_total":2},
          {"paused":false,"resets_left":-1},
          {"paused":false,"resets_left":"1"},
          null
        ]}
        """)
        let snapshot = try PortableClaudeProvider.parse(data, credentials: self.credentials, now: self.now)
        #expect(snapshot.windows.first?.usedPercent == 25)
        #expect(snapshot.provider == .claude)
        let resets = try #require(snapshot.resetCredits)
        #expect(resets.availableCount == 3)
        #expect(resets.credits.count == 3)
        #expect(resets.credits.first?.expiresAt != nil)
        #expect(resets.credits.last?.expiresAt == nil)
        let json = String(decoding: try JSONEncoder().encode(snapshot), as: UTF8.self)
        #expect(!json.contains("private-"))
        #expect(!json.contains("cedar_ember"))
    }

    @Test(arguments: ["null", "{}", "42", "{\"eligible\":false,\"grants\":[]}",
                      "{\"eligible\":true,\"grants\":\"bad\"}"])
    func `bad optional reset inventory does not hide quotas`(inventory: String) throws {
        let snapshot = try PortableClaudeProvider.parse(
            self.payload(inventory), credentials: self.credentials, now: self.now)
        #expect(snapshot.windows.count == 1)
        #expect(snapshot.resetCredits == nil)
    }

    @Test(arguments: [50, 51, 200, 201])
    func `inventory bounds prevent excessive counts and grant arrays`(count: Int) {
        let oneGrant = self.payload("{\"eligible\":true,\"grants\":[{\"paused\":false,\"resets_left\":\(count)}]}")
        #expect(PortableClaudeResetCredits.parse(oneGrant, now: self.now)?.availableCount == (count <= 50 ? count : nil))
        let records = ([#"{"paused":false,"resets_left":1}"#] + Array(repeating: "{}", count: count - 1))
            .joined(separator: ",")
        let manyGrants = self.payload("{\"eligible\":true,\"grants\":[\(records)]}")
        #expect(PortableClaudeResetCredits.parse(manyGrants, now: self.now)?.availableCount == (count <= 200 ? 1 : nil))
    }

    @Test
    func `OAuth requests reset inventory with the same account and no cached response`() async throws {
        let transport = ResetTransport(status: 200, data: self.payload(
            #"{"eligible":true,"grants":[{"paused":false,"resets_left":1}]}"#))
        let snapshot = try await PortableClaudeProvider.fetch(
            credentials: self.credentials, claudeCodeVersion: "2.1.0", transport: transport, now: self.now)
        #expect(snapshot.resetCredits?.availableCount == 1)
        let requests = await transport.requests
        #expect(requests.count == 1)
        #expect(requests[0].url?.query == "cedar_ember=1")
        #expect(requests[0].httpMethod == "GET")
        #expect(requests[0].value(forHTTPHeaderField: "Authorization") == "Bearer fixture-token")
        #expect(requests[0].value(forHTTPHeaderField: "User-Agent") == "claude-cli/2.1.0 (external, cli)")
        #expect(requests[0].cachePolicy == .reloadIgnoringLocalCacheData)
    }

    @Test(arguments: [400, 403])
    func `unsupported query falls back once to ordinary usage`(status: Int) async throws {
        let transport = ResetTransport(status: status, data: self.payload("null"))
        let snapshot = try await PortableClaudeProvider.fetch(credentials: self.credentials, transport: transport)
        #expect(snapshot.windows.first?.usedPercent == 25)
        #expect(snapshot.resetCredits == nil)
        let requests = await transport.requests
        #expect(requests.map { $0.url?.query } == ["cedar_ember=1", nil])
        #expect(requests[1].value(forHTTPHeaderField: "User-Agent") == "claude-code/2.1.0")
        #expect(requests.allSatisfy { $0.value(forHTTPHeaderField: "Authorization") == "Bearer fixture-token" })
    }

    @Test(arguments: [401, 429, 500])
    func `terminal HTTP failures never trigger optional-query retry`(status: Int) async {
        let transport = ResetTransport(status: status, data: self.payload("null"))
        await #expect(throws: PortableProviderError.self) {
            try await PortableClaudeProvider.fetch(credentials: self.credentials, transport: transport)
        }
        #expect(await transport.requests.count == 1)
    }

    private func payload(_ inventory: String) -> Data {
        Data("{\"five_hour\":{\"utilization\":25},\"cedar_ember\":\(inventory)}".utf8)
    }

    @Test
    func `missing profile scope does not retry the request`() async {
        let transport = ResetTransport(status: 403, data: Data(#"{"error":"Missing required scope user:profile"}"#.utf8))
        await #expect(throws: PortableProviderError.self) {
            try await PortableClaudeProvider.fetch(credentials: self.credentials, transport: transport)
        }
        #expect(await transport.requests.count == 1)
    }

    @Test
    func `reset start and expiry boundaries are respected`() {
        let data = self.payload(#"{"eligible":true,"grants":[{"paused":false,"resets_left":1,"starts_at":"2026-11-10T12:00:00Z","ends_at":"2026-11-10T13:00:00Z"}]}"#)
        let start = ISO8601DateFormatter().date(from: "2026-11-10T12:00:00Z")!
        #expect(PortableClaudeResetCredits.parse(data, now: start.addingTimeInterval(-1))?.availableCount == 0)
        #expect(PortableClaudeResetCredits.parse(data, now: start)?.availableCount == 1)
        #expect(PortableClaudeResetCredits.parse(data, now: start.addingTimeInterval(3600))?.availableCount == 0)
    }
}

private actor ResetTransport: PortableHTTPTransport {
    let status: Int
    let data: Data
    var requests: [URLRequest] = []

    init(status: Int, data: Data) {
        self.status = status
        self.data = data
    }

    func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        self.requests.append(request)
        return PortableHTTPResponse(data: self.data, statusCode: request.url?.query == nil ? 200 : self.status)
    }
}
