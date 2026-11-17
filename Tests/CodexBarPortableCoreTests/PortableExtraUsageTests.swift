import Foundation
import Testing
@testable import CodexBarPortableCore

struct PortableExtraUsageTests {
    @Test
    func `Claude extra usage maps minor USD units without inventing a prepaid wallet`() {
        let extra = PortableExtraUsageParser.claude(Data(#"{"extra_usage":{"is_enabled":true,"used_credits":525,"monthly_limit":2000}}"#.utf8))
        #expect(extra?.currency == "USD")
        #expect(extra?.used == 5.25)
        #expect(extra?.limit == 20)
        #expect(extra?.balance == nil)
        #expect(extra?.unlimited == false)
    }

    @Test
    func `a missing monthly cap never becomes unlimited or a zero spend estimate`() {
        let extra = PortableExtraUsageParser.claude(Data(#"{"extra_usage":{"is_enabled":true,"used_credits":100,"monthly_limit":null}}"#.utf8))
        #expect(extra?.used == 1)
        #expect(extra?.limit == nil)
        #expect(extra?.unlimited == false)
        let unknown = PortableExtraUsageParser.claude(Data(#"{"extra_usage":{"is_enabled":true,"used_credits":null,"monthly_limit":2000}}"#.utf8))
        #expect(unknown?.used == nil)
        let enabled = PortableExtraUsageParser.claude(Data(#"{"extra_usage":{"is_enabled":true,"used_credits":null,"monthly_limit":null}}"#.utf8))
        #expect(enabled?.isEnabled == true)
        #expect(enabled?.unlimited == false)
    }

    @Test(arguments: ["null", "{}", "{\"is_enabled\":false,\"used_credits\":5,\"monthly_limit\":20}",
                      "{\"is_enabled\":true,\"used_credits\":-1,\"monthly_limit\":20}",
                      "{\"is_enabled\":true,\"used_credits\":\"NaN\",\"monthly_limit\":20}",
                      "{\"is_enabled\":true,\"used_credits\":5,\"monthly_limit\":20,\"currency\":\"EUR\"}"])
    func `unavailable malformed or non USD data never masquerades as USD`(block: String) {
        #expect(PortableExtraUsageParser.claude(Data("{\"extra_usage\":\(block)}".utf8)) == nil)
    }

    @Test(arguments: ["150.25", "\"150.25\""])
    func `Codex balances remain credits with no fabricated dollar conversion or denominator`(balance: String) {
        let extra = PortableExtraUsageParser.codex(Data("{\"credits\":{\"has_credits\":true,\"unlimited\":false,\"balance\":\(balance)}}".utf8))
        #expect(extra?.currency == "credits")
        #expect(extra?.balance == 150.25)
        #expect(extra?.used == nil)
        #expect(extra?.limit == nil)
    }

    @Test
    func `unlimited and exhausted Codex credit balances retain their explicit flags`() {
        let unlimited = PortableExtraUsageParser.codex(Data(#"{"credits":{"has_credits":true,"unlimited":true,"balance":null}}"#.utf8))
        #expect(unlimited?.unlimited == true)
        #expect(unlimited?.balance == nil)
        let exhausted = PortableExtraUsageParser.codex(Data(#"{"credits":{"has_credits":false,"unlimited":false,"balance":"0"}}"#.utf8))
        #expect(exhausted?.balance == 0)
        #expect(exhausted?.isEnabled == false)
    }

    @Test
    func `optional extra usage survives snapshots and reset attachment while malformed billing preserves quotas`() throws {
        let credentials = PortableCodexCredentials(accessToken: "fixture", refreshToken: nil, idToken: nil, accountID: nil, lastRefresh: nil)
        let good = Data(#"{"rate_limit":{"primary_window":{"used_percent":100}},"credits":{"has_credits":true,"unlimited":false,"balance":"12.5"}}"#.utf8)
        let snapshot = try PortableCodexProvider.parse(good, credentials: credentials)
        #expect(snapshot.withResetCredits(nil).extraUsage?.balance == 12.5)
        let bad = Data(#"{"rate_limit":{"primary_window":{"used_percent":30}},"credits":{"has_credits":true,"unlimited":false,"balance":"bad"}}"#.utf8)
        let result = try PortableCodexProvider.parse(bad, credentials: credentials)
        #expect(result.windows.first?.usedPercent == 30)
        #expect(result.extraUsage == nil)
    }

    @Test
    func `Claude spend only accounts need no invented session quota`() throws {
        let credentials = PortableClaudeCredentials(accessToken: "fixture", refreshToken: nil, expiresAt: nil, rateLimitTier: nil, subscriptionType: "pro")
        let data = Data(#"{"five_hour":null,"seven_day":null,"extra_usage":{"is_enabled":true,"monthly_limit":2000,"used_credits":500}}"#.utf8)
        let snapshot = try PortableClaudeProvider.parse(data, credentials: credentials)
        #expect(snapshot.windows.isEmpty)
        #expect(snapshot.extraUsage?.used == 5)
    }
}
