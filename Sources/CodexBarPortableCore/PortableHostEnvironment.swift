import Foundation

public struct PortableHostEnvironment: Sendable {
    public let values: [String: String]
    public let homeDirectory: URL

    public init(
        values: [String: String] = ProcessInfo.processInfo.environment,
        fileManager: FileManager = .default)
    {
        self.values = values
        self.homeDirectory = Self.resolveHomeDirectory(values: values, fileManager: fileManager)
    }

    public static func resolveHomeDirectory(
        values: [String: String],
        fileManager: FileManager = .default) -> URL
    {
        #if os(Windows)
        if let userProfile = self.nonEmpty(values["USERPROFILE"]) {
            return URL(fileURLWithPath: userProfile, isDirectory: true)
        }
        if let drive = self.nonEmpty(values["HOMEDRIVE"]),
           let path = self.nonEmpty(values["HOMEPATH"])
        {
            return URL(fileURLWithPath: drive + path, isDirectory: true)
        }
        #endif

        if let home = self.nonEmpty(values["HOME"]) {
            return URL(fileURLWithPath: home, isDirectory: true)
        }
        return fileManager.homeDirectoryForCurrentUser
    }

    public var codexHomeDirectory: URL {
        if let override = Self.nonEmpty(self.values["CODEX_HOME"]) {
            return URL(fileURLWithPath: override, isDirectory: true)
        }
        return self.homeDirectory.appendingPathComponent(".codex", isDirectory: true)
    }

    public var codexCredentialsURL: URL {
        self.codexHomeDirectory.appendingPathComponent("auth.json", isDirectory: false)
    }

    public var codexConfigURL: URL {
        self.codexHomeDirectory.appendingPathComponent("config.toml", isDirectory: false)
    }

    public var claudeConfigDirectory: URL {
        if let override = Self.nonEmpty(self.values["CLAUDE_CONFIG_DIR"]) {
            return URL(fileURLWithPath: override, isDirectory: true)
        }
        return self.homeDirectory.appendingPathComponent(".claude", isDirectory: true)
    }

    public var claudeCredentialsURL: URL {
        self.claudeConfigDirectory.appendingPathComponent(".credentials.json", isDirectory: false)
    }

    public var applicationDataDirectory: URL {
        #if os(Windows)
        if let localAppData = Self.nonEmpty(self.values["LOCALAPPDATA"]) {
            return URL(fileURLWithPath: localAppData, isDirectory: true)
                .appendingPathComponent("CodexBar", isDirectory: true)
        }
        #endif

        if let xdgConfigHome = Self.nonEmpty(self.values["XDG_CONFIG_HOME"]) {
            return URL(fileURLWithPath: xdgConfigHome, isDirectory: true)
                .appendingPathComponent("codexbar", isDirectory: true)
        }
        return self.homeDirectory
            .appendingPathComponent(".config", isDirectory: true)
            .appendingPathComponent("codexbar", isDirectory: true)
    }

    private static func nonEmpty(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
