import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

enum PortableCodexProvider {
    private static let defaultBaseURL = "https://chatgpt.com/backend-api"
    private static let usagePath = "/wham/usage"

    static func fetch(
        credentials: PortableCodexCredentials,
        environment: PortableHostEnvironment,
        transport: any PortableHTTPTransport = PortableURLSessionTransport(),
        now: Date = Date()) async throws -> PortableProviderSnapshot
    {
        let url = self.usageURL(environment: environment)
        var request = URLRequest(
            url: url,
            cachePolicy: .reloadIgnoringLocalCacheData,
            timeoutInterval: 30)
        request.httpMethod = "GET"
        request.setValue("Bearer \(credentials.accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue("CodexBar-Windows", forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let accountID = credentials.accountID, !accountID.isEmpty {
            request.setValue(accountID, forHTTPHeaderField: "ChatGPT-Account-Id")
        }

        let response: PortableHTTPResponse
        do {
            response = try await transport.response(for: request)
        } catch let error as PortableProviderError {
            throw error
        } catch {
            throw PortableProviderError.network(provider: .codex, details: error.localizedDescription)
        }

        switch response.statusCode {
        case 200...299:
            return try self.parse(
                response.data,
                credentials: credentials,
                now: now)
        case 401, 403:
            throw PortableProviderError.unauthorized(provider: .codex)
        default:
            throw PortableProviderError.server(provider: .codex, statusCode: response.statusCode)
        }
    }

    static func parse(
        _ data: Data,
        credentials: PortableCodexCredentials,
        now: Date = Date()) throws -> PortableProviderSnapshot
    {
        guard let payload = try? JSONDecoder().decode(PortableCodexUsageResponse.self, from: data) else {
            throw PortableProviderError.invalidResponse(provider: .codex)
        }

        let windows: [PortableRateWindow] = [
            self.window(payload.rateLimit?.primaryWindow, id: "session", label: "Session"),
            self.window(payload.rateLimit?.secondaryWindow, id: "weekly", label: "Weekly"),
        ].compactMap(\.self)
        guard !windows.isEmpty else {
            throw PortableProviderError.invalidResponse(provider: .codex)
        }

        let claims = credentials.idToken.flatMap(self.jwtClaims)
        let email = self.stringClaim(
            claims,
            directKey: "email",
            namespace: "https://api.openai.com/profile")
        let tokenPlan = self.stringClaim(
            claims,
            directKey: "chatgpt_plan_type",
            namespace: "https://api.openai.com/auth")
        let identity = PortableProviderIdentity(
            accountEmail: email,
            plan: payload.planType ?? tokenPlan)

        return PortableProviderSnapshot(
            provider: .codex,
            source: "oauth",
            windows: windows,
            identity: identity,
            updatedAt: now)
    }

    static func usageURL(environment: PortableHostEnvironment) -> URL {
        let configured = (try? String(contentsOf: environment.codexConfigURL, encoding: .utf8))
            .flatMap(self.chatGPTBaseURL)
        var base = configured ?? Self.defaultBaseURL
        while base.hasSuffix("/") {
            base.removeLast()
        }
        if base.hasPrefix("https://chatgpt.com") || base.hasPrefix("https://chat.openai.com"),
           !base.contains("/backend-api")
        {
            base += "/backend-api"
        }
        let path = base.contains("/backend-api") ? Self.usagePath : "/api/codex/usage"
        return URL(string: base + path) ?? URL(string: Self.defaultBaseURL + Self.usagePath)!
    }

    private static func window(
        _ value: PortableCodexUsageResponse.RateLimit.Window?,
        id: String,
        label: String) -> PortableRateWindow?
    {
        guard let value else { return nil }
        return PortableRateWindow(
            id: id,
            label: label,
            usedPercent: value.usedPercent,
            windowMinutes: value.limitWindowSeconds.map { $0 / 60 },
            resetsAt: value.resetAt.map { Date(timeIntervalSince1970: $0) })
    }

    private static func chatGPTBaseURL(_ contents: String) -> String? {
        for rawLine in contents.split(whereSeparator: \.isNewline) {
            let withoutComment = rawLine.split(separator: "#", maxSplits: 1).first ?? rawLine
            let parts = withoutComment.split(separator: "=", maxSplits: 1)
            guard parts.count == 2,
                  parts[0].trimmingCharacters(in: .whitespacesAndNewlines) == "chatgpt_base_url"
            else {
                continue
            }
            return parts[1]
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
        }
        return nil
    }

    private static func jwtClaims(_ token: String) -> [String: Any]? {
        let segments = token.split(separator: ".")
        guard segments.count >= 2 else { return nil }
        var encoded = String(segments[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        let remainder = encoded.count % 4
        if remainder != 0 {
            encoded += String(repeating: "=", count: 4 - remainder)
        }
        guard let data = Data(base64Encoded: encoded) else { return nil }
        return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
    }

    private static func stringClaim(
        _ claims: [String: Any]?,
        directKey: String,
        namespace: String) -> String?
    {
        if let direct = claims?[directKey] as? String, !direct.isEmpty {
            return direct
        }
        return (claims?[namespace] as? [String: Any])?[directKey] as? String
    }
}

private struct PortableCodexUsageResponse: Decodable {
    struct RateLimit: Decodable {
        struct Window: Decodable {
            let usedPercent: Double
            let resetAt: Double?
            let limitWindowSeconds: Int?

            enum CodingKeys: String, CodingKey {
                case usedPercent = "used_percent"
                case resetAt = "reset_at"
                case limitWindowSeconds = "limit_window_seconds"
            }
        }

        let primaryWindow: Window?
        let secondaryWindow: Window?

        enum CodingKeys: String, CodingKey {
            case primaryWindow = "primary_window"
            case secondaryWindow = "secondary_window"
        }

        init(from decoder: Decoder) throws {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            self.primaryWindow = try? container.decodeIfPresent(Window.self, forKey: .primaryWindow)
            self.secondaryWindow = try? container.decodeIfPresent(Window.self, forKey: .secondaryWindow)
        }
    }

    let planType: String?
    let rateLimit: RateLimit?

    enum CodingKeys: String, CodingKey {
        case planType = "plan_type"
        case rateLimit = "rate_limit"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.planType = try? container.decodeIfPresent(String.self, forKey: .planType)
        self.rateLimit = try? container.decodeIfPresent(RateLimit.self, forKey: .rateLimit)
    }
}
