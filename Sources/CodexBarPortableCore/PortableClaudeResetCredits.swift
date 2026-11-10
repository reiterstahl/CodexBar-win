import Foundation

/// Read-only saved resets from the same OAuth response/account as ordinary usage.
/// Redemption identifiers and grant labels are deliberately not decoded or exported.
enum PortableClaudeResetCredits {
    static func parse(_ data: Data, now: Date) -> PortableResetCredits? {
        guard let envelope = try? JSONDecoder().decode(Envelope.self, from: data),
              let inventory = envelope.cedarEmber, inventory.eligible
        else { return nil }

        var credits: [PortableResetCredit] = []
        for grant in inventory.grants {
            guard !grant.paused, grant.remaining > 0,
                  grant.startsAt.map({ $0 <= now }) ?? true,
                  grant.endsAt.map({ $0 > now }) ?? true
            else { continue }
            guard grant.remaining <= 50 - credits.count else { return nil }
            credits.append(contentsOf: repeatElement(
                PortableResetCredit(expiresAt: grant.endsAt, title: nil), count: grant.remaining))
        }
        credits.sort {
            switch ($0.expiresAt, $1.expiresAt) {
            case let (left?, right?): left < right
            case (_?, nil): true
            default: false
            }
        }
        return PortableResetCredits(availableCount: credits.count, credits: credits)
    }

    private struct Envelope: Decodable {
        let cedarEmber: Inventory?
        enum CodingKeys: String, CodingKey { case cedarEmber = "cedar_ember" }
    }

    private struct Inventory: Decodable {
        let eligible: Bool
        let grants: [Grant]
        enum CodingKeys: String, CodingKey { case eligible, grants }

        init(from decoder: Decoder) throws {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            self.eligible = try container.decode(Bool.self, forKey: .eligible)
            var records = try container.nestedUnkeyedContainer(forKey: .grants)
            var valid: [Grant] = []
            while !records.isAtEnd {
                guard records.currentIndex < 200 else {
                    throw DecodingError.dataCorruptedError(in: records, debugDescription: "Too many reset grants")
                }
                // Advance past an entire record, even if its fields are malformed.
                let record = try records.superDecoder()
                if let grant = try? Grant(from: record) { valid.append(grant) }
            }
            self.grants = valid
        }
    }

    private struct Grant: Decodable {
        let remaining: Int
        let paused: Bool
        let startsAt: Date?
        let endsAt: Date?
        enum CodingKeys: String, CodingKey {
            case remaining = "resets_left", total = "resets_total", paused
            case startsAt = "starts_at", endsAt = "ends_at"
        }

        init(from decoder: Decoder) throws {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            let remaining = try container.decode(Int.self, forKey: .remaining)
            self.remaining = remaining
            self.paused = try container.decode(Bool.self, forKey: .paused)
            let total = try container.decodeIfPresent(Int.self, forKey: .total)
            guard remaining >= 0, total.map({ $0 >= remaining }) ?? true else {
                throw DecodingError.dataCorruptedError(
                    forKey: .remaining, in: container, debugDescription: "Invalid reset count")
            }
            self.startsAt = try Self.date(container, key: .startsAt)
            self.endsAt = try Self.date(container, key: .endsAt)
        }

        private static func date(_ container: KeyedDecodingContainer<CodingKeys>, key: CodingKeys) throws -> Date? {
            guard let raw = try container.decodeIfPresent(String.self, forKey: key) else { return nil }
            let formatter = ISO8601DateFormatter()
            formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
            if let date = formatter.date(from: raw) ?? ISO8601DateFormatter().date(from: raw) { return date }
            throw DecodingError.dataCorruptedError(
                forKey: key, in: container, debugDescription: "Invalid reset date")
        }
    }
}
