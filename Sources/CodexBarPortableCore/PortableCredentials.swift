import Foundation

enum PortableCredentialError: LocalizedError, Sendable, Equatable {
    case notFound(provider: PortableProvider, path: String)
    case unreadable(provider: PortableProvider, details: String)
    case invalid(provider: PortableProvider, details: String)
    case expired(provider: PortableProvider)

    var errorDescription: String? {
        switch self {
        case let .notFound(provider, path):
            "\(provider.displayName) credentials were not found at \(path). Run `\(provider.rawValue)` to sign in."
        case let .unreadable(provider, details):
            "\(provider.displayName) credentials could not be read: \(details)"
        case let .invalid(provider, details):
            "\(provider.displayName) credentials are invalid: \(details)"
        case let .expired(provider):
            "\(provider.displayName) credentials expired. Run `\(provider.rawValue)` to refresh the login."
        }
    }
}

public protocol PortableCredentialFileReading: Sendable {
    func data(at url: URL) throws -> Data
}

public protocol PortableCredentialFileWriting: Sendable {
    func replace(_ data: Data, at url: URL) throws
}

public struct PortableCredentialFileReader: PortableCredentialFileReading {
    public init() {}

    public func data(at url: URL) throws -> Data {
        guard FileManager.default.fileExists(atPath: url.path) else {
            throw PortableCredentialFileReadError.notFound
        }
        return try Data(contentsOf: url)
    }
}

public struct PortableCredentialFileWriter: PortableCredentialFileWriting {
    public init() {}

    public func replace(_ data: Data, at url: URL) throws {
        try data.write(to: url, options: .atomic)
    }
}

private enum PortableCredentialFileReadError: Error {
    case notFound
}

struct PortableCodexCredentials: Equatable, Sendable {
    let accessToken: String
    let refreshToken: String?
    let idToken: String?
    let accountID: String?
    let lastRefresh: Date?
}

struct PortableClaudeCredentials: Equatable, Sendable {
    let accessToken: String
    let refreshToken: String?
    let expiresAt: Date?
    let rateLimitTier: String?
    let subscriptionType: String?
}

enum PortableCredentialLoader {
    static func loadCodex(
        environment: PortableHostEnvironment,
        reader: any PortableCredentialFileReading = PortableCredentialFileReader()) throws
        -> PortableCodexCredentials
    {
        let url = environment.codexCredentialsURL
        let data = try self.read(.codex, url: url, reader: reader)
        return try self.parseCodex(data)
    }

    static func loadClaude(
        environment: PortableHostEnvironment,
        reader: any PortableCredentialFileReading = PortableCredentialFileReader()) throws -> PortableClaudeCredentials
    {
        let url = environment.claudeCredentialsURL
        let data = try self.read(.claude, url: url, reader: reader)
        return try self.parseClaude(data)
    }

    static func saveClaude(
        _ credentials: PortableClaudeCredentials,
        environment: PortableHostEnvironment,
        reader: any PortableCredentialFileReading,
        writer: any PortableCredentialFileWriting) throws
    {
        let url = environment.claudeCredentialsURL
        let original = try self.read(.claude, url: url, reader: reader)
        guard var root = try? JSONSerialization.jsonObject(with: original) as? [String: Any],
              var oauth = root["claudeAiOauth"] as? [String: Any]
        else {
            throw PortableCredentialError.invalid(provider: .claude, details: "malformed JSON")
        }

        oauth["accessToken"] = credentials.accessToken
        if let refreshToken = credentials.refreshToken {
            oauth["refreshToken"] = refreshToken
        }
        if let expiresAt = credentials.expiresAt {
            oauth["expiresAt"] = Int64((expiresAt.timeIntervalSince1970 * 1_000).rounded())
        }
        root["claudeAiOauth"] = oauth

        do {
            let updated = try JSONSerialization.data(withJSONObject: root, options: [.sortedKeys])
            try writer.replace(updated, at: url)
        } catch {
            throw PortableCredentialError.unreadable(provider: .claude, details: error.localizedDescription)
        }
    }

    static func parseCodex(_ data: Data) throws -> PortableCodexCredentials {
        guard let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw PortableCredentialError.invalid(provider: .codex, details: "malformed JSON")
        }

        if let apiKey = self.nonEmpty(root["OPENAI_API_KEY"] as? String) {
            return PortableCodexCredentials(
                accessToken: apiKey,
                refreshToken: nil,
                idToken: nil,
                accountID: nil,
                lastRefresh: nil)
        }

        guard let tokens = root["tokens"] as? [String: Any],
              let accessToken = self.value(tokens, snake: "access_token", camel: "accessToken")
        else {
            throw PortableCredentialError.invalid(provider: .codex, details: "missing access token")
        }

        return PortableCodexCredentials(
            accessToken: accessToken,
            refreshToken: self.value(tokens, snake: "refresh_token", camel: "refreshToken"),
            idToken: self.value(tokens, snake: "id_token", camel: "idToken"),
            accountID: self.value(tokens, snake: "account_id", camel: "accountId"),
            lastRefresh: self.parseISO8601(root["last_refresh"] as? String))
    }

    static func parseClaude(_ data: Data) throws -> PortableClaudeCredentials {
        struct Root: Decodable {
            struct OAuth: Decodable {
                let accessToken: String?
                let refreshToken: String?
                let expiresAt: Double?
                let rateLimitTier: String?
                let subscriptionType: String?
            }

            let claudeAiOauth: OAuth?
        }

        guard let root = try? JSONDecoder().decode(Root.self, from: data) else {
            throw PortableCredentialError.invalid(provider: .claude, details: "malformed JSON")
        }
        guard let oauth = root.claudeAiOauth,
              let accessToken = self.nonEmpty(oauth.accessToken)
        else {
            throw PortableCredentialError.invalid(provider: .claude, details: "missing claudeAiOauth access token")
        }

        return PortableClaudeCredentials(
            accessToken: accessToken,
            refreshToken: self.nonEmpty(oauth.refreshToken),
            expiresAt: oauth.expiresAt.map { Date(timeIntervalSince1970: $0 / 1000) },
            rateLimitTier: self.nonEmpty(oauth.rateLimitTier),
            subscriptionType: self.nonEmpty(oauth.subscriptionType))
    }

    private static func read(
        _ provider: PortableProvider,
        url: URL,
        reader: any PortableCredentialFileReading) throws -> Data
    {
        do {
            return try reader.data(at: url)
        } catch PortableCredentialFileReadError.notFound {
            throw PortableCredentialError.notFound(provider: provider, path: url.path)
        } catch let error as CocoaError where error.code == .fileReadNoSuchFile {
            throw PortableCredentialError.notFound(provider: provider, path: url.path)
        } catch {
            throw PortableCredentialError.unreadable(provider: provider, details: error.localizedDescription)
        }
    }

    private static func value(_ dictionary: [String: Any], snake: String, camel: String) -> String? {
        self.nonEmpty(dictionary[snake] as? String) ?? self.nonEmpty(dictionary[camel] as? String)
    }

    private static func nonEmpty(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
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
}
