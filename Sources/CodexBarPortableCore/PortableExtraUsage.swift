import Foundation

enum PortableExtraUsageParser {
    static func claude(_ data: Data) -> PortableExtraUsage? {
        guard let payload = try? JSONDecoder().decode(ClaudeEnvelope.self, from: data),
              let extra = payload.extraUsage, extra.isEnabled,
              extra.currency == nil || extra.currency?.uppercased() == "USD"
        else { return nil }
        return PortableExtraUsage(
            currency: "USD", used: extra.used.map { $0.value / 100 },
            limit: extra.limit.map { $0.value / 100 }, balance: nil,
            isEnabled: true, unlimited: false)
    }

    static func codex(_ data: Data) -> PortableExtraUsage? {
        guard let payload = try? JSONDecoder().decode(CodexEnvelope.self, from: data),
              let credits = payload.credits,
              credits.hasCredits || credits.unlimited || credits.balance != nil
        else { return nil }
        return PortableExtraUsage(
            currency: "credits", used: nil, limit: nil, balance: credits.balance?.value,
            isEnabled: credits.hasCredits || credits.unlimited, unlimited: credits.unlimited)
    }

    private struct Number: Decodable {
        let value: Double
        init(from decoder: Decoder) throws {
            let container = try decoder.singleValueContainer()
            let value: Double? = (try? container.decode(Double.self))
                ?? (try? container.decode(String.self)).flatMap(Double.init)
            guard let value, value.isFinite, value >= 0 else {
                throw DecodingError.dataCorruptedError(in: container, debugDescription: "Invalid extra usage amount")
            }
            self.value = value
        }
    }

    private struct ClaudeEnvelope: Decodable {
        let extraUsage: ClaudeExtra?
        enum CodingKeys: String, CodingKey { case extraUsage = "extra_usage" }
    }

    private struct ClaudeExtra: Decodable {
        let isEnabled: Bool
        let used: Number?
        let limit: Number?
        let currency: String?
        enum CodingKeys: String, CodingKey {
            case isEnabled = "is_enabled", used = "used_credits", limit = "monthly_limit", currency
        }
    }

    private struct CodexEnvelope: Decodable {
        let credits: CodexCredits?
    }

    private struct CodexCredits: Decodable {
        let hasCredits: Bool
        let unlimited: Bool
        let balance: Number?
        enum CodingKeys: String, CodingKey { case hasCredits = "has_credits", unlimited, balance }
    }
}
