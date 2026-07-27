import Foundation

public struct CodexBarPortableEngine: Sendable {
    private let environment: PortableHostEnvironment
    private let credentialReader: any PortableCredentialFileReading
    private let transport: any PortableHTTPTransport
    private let now: @Sendable () -> Date

    public init(
        environment: PortableHostEnvironment = PortableHostEnvironment(),
        credentialReader: any PortableCredentialFileReading = PortableCredentialFileReader(),
        transport: any PortableHTTPTransport = PortableURLSessionTransport(),
        now: @escaping @Sendable () -> Date = Date.init)
    {
        self.environment = environment
        self.credentialReader = credentialReader
        self.transport = transport
        self.now = now
    }

    public func snapshot(
        providers requestedProviders: [PortableProvider] = PortableProvider.allCases) async
        -> PortableEngineSnapshot
    {
        let providers = self.normalizedProviders(requestedProviders)
        var snapshots: [PortableProviderSnapshot] = []
        var failures: [PortableProviderFailure] = []

        // Fetch sequentially for now. There are only two providers, and this avoids coupling
        // independent credential failures through sibling child-task cancellation.
        for provider in providers {
            do {
                snapshots.append(try await self.fetch(provider))
            } catch {
                failures.append(self.failure(provider: provider, error: error))
            }
        }

        return PortableEngineSnapshot(
            generatedAt: self.now(),
            providers: snapshots,
            failures: failures)
    }

    private func fetch(_ provider: PortableProvider) async throws -> PortableProviderSnapshot {
        switch provider {
        case .codex:
            let credentials = try PortableCredentialLoader.loadCodex(
                environment: self.environment,
                reader: self.credentialReader)
            return try await PortableCodexProvider.fetch(
                credentials: credentials,
                environment: self.environment,
                transport: self.transport,
                now: self.now())
        case .claude:
            let credentials = try PortableCredentialLoader.loadClaude(
                environment: self.environment,
                reader: self.credentialReader,
                now: self.now())
            let version = self.environment.values["CLAUDE_CODE_VERSION"]
            return try await PortableClaudeProvider.fetch(
                credentials: credentials,
                claudeCodeVersion: version,
                transport: self.transport,
                now: self.now())
        }
    }

    private func normalizedProviders(_ providers: [PortableProvider]) -> [PortableProvider] {
        var seen: Set<PortableProvider> = []
        return providers.filter { seen.insert($0).inserted }
    }

    private func failure(provider: PortableProvider, error: Error) -> PortableProviderFailure {
        let code: String
        switch error {
        case let credentialError as PortableCredentialError:
            code = switch credentialError {
            case .notFound: "credentials_not_found"
            case .unreadable: "credentials_unreadable"
            case .invalid: "credentials_invalid"
            case .expired: "credentials_expired"
            }
        case let providerError as PortableProviderError:
            code = switch providerError {
            case .unauthorized: "unauthorized"
            case .invalidResponse: "invalid_response"
            case .server: "server_error"
            case .network: "network_error"
            }
        default:
            code = "unknown"
        }
        return PortableProviderFailure(
            provider: provider,
            code: code,
            message: error.localizedDescription)
    }
}
