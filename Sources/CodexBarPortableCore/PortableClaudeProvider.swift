import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

enum PortableClaudeProvider {
    private static let usageURL = URL(string: "https://api.anthropic.com/api/oauth/usage")!
    private static let betaHeader = "oauth-2025-04-20"
    private static let fallbackVersion = "2.1.0"

    static func fetch(
        credentials: PortableClaudeCredentials,
        claudeCodeVersion: String? = nil,
        transport: any PortableHTTPTransport = PortableURLSessionTransport(),
        now: Date = Date()) async throws -> PortableProviderSnapshot
    {
        var request = URLRequest(
            url: Self.usageURL,
            cachePolicy: .reloadIgnoringLocalCacheData,
            timeoutInterval: 30)
        request.httpMethod = "GET"
        request.setValue("Bearer \(credentials.accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(Self.betaHeader, forHTTPHeaderField: "anthropic-beta")
        request.setValue(
            "claude-code/\(self.normalizedVersion(claudeCodeVersion) ?? Self.fallbackVersion)",
            forHTTPHeaderField: "User-Agent")

        let response: PortableHTTPResponse
        do {
            response = try await transport.response(for: request)
        } catch let error as PortableProviderError {
            throw error
        } catch {
            throw PortableProviderError.network(provider: .claude, details: error.localizedDescription)
        }

        switch response.statusCode {
        case 200:
            return try self.parse(response.data, credentials: credentials, now: now)
        case 401, 403:
            throw PortableProviderError.unauthorized(provider: .claude)
        default:
            throw PortableProviderError.server(provider: .claude, statusCode: response.statusCode)
        }
    }

    static func parse(
        _ data: Data,
        credentials: PortableClaudeCredentials,
        now: Date = Date()) throws -> PortableProviderSnapshot
    {
        guard let payload = try? JSONDecoder().decode(PortableClaudeUsageResponse.self, from: data) else {
            throw PortableProviderError.invalidResponse(provider: .claude)
        }

        var windows: [PortableRateWindow] = []
        if let session = self.window(
            payload.fiveHour,
            id: "session",
            label: "Session",
            windowMinutes: 5 * 60)
        {
            windows.append(session)
        }
        if let weekly = self.window(
            payload.sevenDay,
            id: "weekly",
            label: "Weekly",
            windowMinutes: 7 * 24 * 60)
        {
            windows.append(weekly)
        }
        if let sonnet = self.window(
            payload.sevenDaySonnet,
            id: "weekly-sonnet",
            label: "Sonnet",
            windowMinutes: 7 * 24 * 60)
        {
            windows.append(sonnet)
        }
        if let opus = self.window(
            payload.sevenDayOpus,
            id: "weekly-opus",
            label: "Opus",
            windowMinutes: 7 * 24 * 60)
        {
            windows.append(opus)
        }

        for limit in payload.limits ?? [] {
            guard limit.kind == "weekly_scoped" || limit.group == "weekly",
                  let percent = limit.percent
            else {
                continue
            }
            let name = self.nonEmpty(limit.scope?.model?.displayName)
                ?? self.nonEmpty(limit.scope?.model?.id)
                ?? "Model"
            windows.append(PortableRateWindow(
                id: "weekly-\(self.slug(name))",
                label: name,
                usedPercent: percent,
                windowMinutes: 7 * 24 * 60,
                resetsAt: self.parseISO8601(limit.resetsAt)))
        }

        guard !windows.isEmpty else {
            throw PortableProviderError.invalidResponse(provider: .claude)
        }

        let identity = PortableProviderIdentity(
            accountEmail: nil,
            plan: self.plan(credentials))
        return PortableProviderSnapshot(
            provider: .claude,
            source: "oauth",
            windows: windows,
            identity: identity,
            updatedAt: now)
    }

    private static func window(
        _ value: PortableClaudeUsageResponse.Window?,
        id: String,
        label: String,
        windowMinutes: Int) -> PortableRateWindow?
    {
        guard let value, let utilization = value.utilization else { return nil }
        return PortableRateWindow(
            id: id,
            label: label,
            usedPercent: utilization,
            windowMinutes: windowMinutes,
            resetsAt: self.parseISO8601(value.resetsAt))
    }

    private static func plan(_ credentials: PortableClaudeCredentials) -> String? {
        self.nonEmpty(credentials.subscriptionType) ?? self.nonEmpty(credentials.rateLimitTier)
    }

    private static func parseISO8601(_ value: String?) -> Date? {
        guard let value = self.nonEmpty(value) else { return nil }
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = fractional.date(from: value) {
            return date
        }
        return ISO8601DateFormatter().date(from: value)
    }

    private static func normalizedVersion(_ value: String?) -> String? {
        guard let raw = self.nonEmpty(value) else { return nil }
        return raw.split(whereSeparator: \.isWhitespace).first.map(String.init)
    }

    private static func nonEmpty(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func slug(_ value: String) -> String {
        let allowed = CharacterSet.alphanumerics
        let scalars = value.lowercased().unicodeScalars.map { allowed.contains($0) ? Character(String($0)) : "-" }
        return String(scalars).split(separator: "-").joined(separator: "-")
    }
}

private struct PortableClaudeUsageResponse: Decodable {
    struct Window: Decodable {
        let utilization: Double?
        let resetsAt: String?

        enum CodingKeys: String, CodingKey {
            case utilization
            case resetsAt = "resets_at"
        }
    }

    struct Limit: Decodable {
        struct Scope: Decodable {
            struct Model: Decodable {
                let id: String?
                let displayName: String?

                enum CodingKeys: String, CodingKey {
                    case id
                    case displayName = "display_name"
                }
            }

            let model: Model?
        }

        let kind: String?
        let group: String?
        let percent: Double?
        let resetsAt: String?
        let scope: Scope?

        enum CodingKeys: String, CodingKey {
            case kind
            case group
            case percent
            case resetsAt = "resets_at"
            case scope
        }
    }

    let fiveHour: Window?
    let sevenDay: Window?
    let sevenDayOpus: Window?
    let sevenDaySonnet: Window?
    let limits: [Limit]?

    enum CodingKeys: String, CodingKey {
        case fiveHour = "five_hour"
        case sevenDay = "seven_day"
        case sevenDayOpus = "seven_day_opus"
        case sevenDaySonnet = "seven_day_sonnet"
        case limits
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.fiveHour = try? container.decodeIfPresent(Window.self, forKey: .fiveHour)
        self.sevenDay = try? container.decodeIfPresent(Window.self, forKey: .sevenDay)
        self.sevenDayOpus = try? container.decodeIfPresent(Window.self, forKey: .sevenDayOpus)
        self.sevenDaySonnet = try? container.decodeIfPresent(Window.self, forKey: .sevenDaySonnet)
        self.limits = try? container.decodeIfPresent([Limit].self, forKey: .limits)
    }
}
