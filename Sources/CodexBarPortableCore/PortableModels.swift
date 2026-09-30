import Foundation

public enum PortableProvider: String, Codable, CaseIterable, Hashable, Sendable {
    case codex
    case claude

    public var displayName: String {
        switch self {
        case .codex:
            "Codex"
        case .claude:
            "Claude Code"
        }
    }
}

public struct PortableRateWindow: Codable, Equatable, Sendable {
    public let id: String
    public let label: String
    public let usedPercent: Double
    public let windowMinutes: Int?
    public let resetsAt: Date?

    public init(
        id: String,
        label: String,
        usedPercent: Double,
        windowMinutes: Int?,
        resetsAt: Date?)
    {
        self.id = id
        self.label = label
        self.usedPercent = min(100, max(0, usedPercent))
        self.windowMinutes = windowMinutes
        self.resetsAt = resetsAt
    }

    public var remainingPercent: Double {
        max(0, 100 - self.usedPercent)
    }
}

public struct PortableProviderIdentity: Codable, Equatable, Sendable {
    public let accountEmail: String?
    public let plan: String?

    public init(accountEmail: String?, plan: String?) {
        self.accountEmail = accountEmail
        self.plan = plan
    }
}

/// One Codex rate-limit reset credit that the account can still redeem in ChatGPT.
public struct PortableResetCredit: Codable, Equatable, Sendable {
    public let expiresAt: Date?
    public let title: String?

    public init(expiresAt: Date?, title: String?) {
        self.expiresAt = expiresAt
        self.title = title
    }
}

/// Available Codex reset credits, soonest expiry first. Read-only: CodexBar never redeems them.
public struct PortableResetCredits: Codable, Equatable, Sendable {
    public let availableCount: Int
    public let credits: [PortableResetCredit]

    public init(availableCount: Int, credits: [PortableResetCredit]) {
        self.availableCount = max(0, availableCount)
        self.credits = credits
    }
}

public struct PortableProviderSnapshot: Codable, Equatable, Sendable {
    public let provider: PortableProvider
    public let displayName: String
    public let source: String
    public let windows: [PortableRateWindow]
    public let identity: PortableProviderIdentity?
    public let updatedAt: Date
    /// Optional extra; absent when the provider has no such concept or the lookup failed.
    public let resetCredits: PortableResetCredits?

    public init(
        provider: PortableProvider,
        source: String,
        windows: [PortableRateWindow],
        identity: PortableProviderIdentity?,
        updatedAt: Date,
        resetCredits: PortableResetCredits? = nil)
    {
        self.provider = provider
        self.displayName = provider.displayName
        self.source = source
        self.windows = windows
        self.identity = identity
        self.updatedAt = updatedAt
        self.resetCredits = resetCredits
    }

    func withResetCredits(_ resetCredits: PortableResetCredits?) -> PortableProviderSnapshot {
        PortableProviderSnapshot(
            provider: self.provider,
            source: self.source,
            windows: self.windows,
            identity: self.identity,
            updatedAt: self.updatedAt,
            resetCredits: resetCredits)
    }
}

public struct PortableProviderFailure: Codable, Equatable, Sendable {
    public let provider: PortableProvider
    public let code: String
    public let message: String

    public init(provider: PortableProvider, code: String, message: String) {
        self.provider = provider
        self.code = code
        self.message = message
    }
}

public struct PortableEngineSnapshot: Codable, Equatable, Sendable {
    public static let schemaVersion = 1

    public let schemaVersion: Int
    public let generatedAt: Date
    public let providers: [PortableProviderSnapshot]
    public let failures: [PortableProviderFailure]

    public init(
        generatedAt: Date,
        providers: [PortableProviderSnapshot],
        failures: [PortableProviderFailure])
    {
        self.schemaVersion = Self.schemaVersion
        self.generatedAt = generatedAt
        self.providers = providers
        self.failures = failures
    }
}
