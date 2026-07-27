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

public struct PortableProviderSnapshot: Codable, Equatable, Sendable {
    public let provider: PortableProvider
    public let displayName: String
    public let source: String
    public let windows: [PortableRateWindow]
    public let identity: PortableProviderIdentity?
    public let updatedAt: Date

    public init(
        provider: PortableProvider,
        source: String,
        windows: [PortableRateWindow],
        identity: PortableProviderIdentity?,
        updatedAt: Date)
    {
        self.provider = provider
        self.displayName = provider.displayName
        self.source = source
        self.windows = windows
        self.identity = identity
        self.updatedAt = updatedAt
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
