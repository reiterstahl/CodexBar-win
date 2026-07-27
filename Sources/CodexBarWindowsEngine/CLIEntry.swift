import CodexBarPortableCore
import Foundation

@main
enum CodexBarWindowsEngineMain {
    static func main() async {
        let arguments = Array(CommandLine.arguments.dropFirst())
        if arguments.contains("--help") || arguments.contains("-h") {
            self.printHelp()
            return
        }
        if arguments.contains("--version") || arguments.contains("-V") {
            print("CodexBarWindowsEngine 0.1.0")
            return
        }

        do {
            let options = try self.parse(arguments)
            let snapshot = await CodexBarPortableEngine().snapshot(providers: options.providers)
            let encoder = JSONEncoder()
            encoder.dateEncodingStrategy = .iso8601
            encoder.outputFormatting = options.pretty ? [.prettyPrinted, .sortedKeys] : [.sortedKeys]
            let data = try encoder.encode(snapshot)
            guard let output = String(data: data, encoding: .utf8) else {
                throw EngineCLIError.encodingFailed
            }
            print(output)
        } catch {
            let payload = EngineCLIErrorPayload(error: error.localizedDescription)
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.sortedKeys]
            if let data = try? encoder.encode(payload),
               let output = String(data: data, encoding: .utf8)
            {
                print(output)
            } else {
                print("{\"error\":\"CodexBarWindowsEngine failed\"}")
            }
        }
    }

    private struct Options {
        let providers: [PortableProvider]
        let pretty: Bool
    }

    private static func parse(_ arguments: [String]) throws -> Options {
        var providers: [PortableProvider] = []
        var pretty = false
        var index = 0

        while index < arguments.count {
            switch arguments[index] {
            case "--pretty":
                pretty = true
            case "--provider":
                index += 1
                guard index < arguments.count else {
                    throw EngineCLIError.missingProvider
                }
                guard let provider = PortableProvider(rawValue: arguments[index].lowercased()) else {
                    throw EngineCLIError.invalidProvider(arguments[index])
                }
                providers.append(provider)
            default:
                throw EngineCLIError.unknownArgument(arguments[index])
            }
            index += 1
        }

        return Options(
            providers: providers.isEmpty ? PortableProvider.allCases : providers,
            pretty: pretty)
    }

    private static func printHelp() {
        print("""
        CodexBarWindowsEngine

        Reads the existing Codex and Claude Code OAuth credentials and emits a JSON usage snapshot.

        Usage:
          CodexBarWindowsEngine [--provider codex] [--provider claude] [--pretty]

        Options:
          --provider <name>  Fetch only codex or claude. Repeat to select both.
          --pretty           Pretty-print JSON.
          -V, --version      Print the engine version.
          -h, --help         Show this help.
        """)
    }
}

private struct EngineCLIErrorPayload: Encodable {
    let error: String
}

private enum EngineCLIError: LocalizedError {
    case missingProvider
    case invalidProvider(String)
    case unknownArgument(String)
    case encodingFailed

    var errorDescription: String? {
        switch self {
        case .missingProvider:
            "Missing value after --provider."
        case let .invalidProvider(value):
            "Unknown provider '\(value)'; expected codex or claude."
        case let .unknownArgument(value):
            "Unknown argument '\(value)'."
        case .encodingFailed:
            "Failed to encode the engine snapshot."
        }
    }
}
