import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

public struct PortableHTTPResponse: Sendable {
    public let data: Data
    public let statusCode: Int

    public init(data: Data, statusCode: Int) {
        self.data = data
        self.statusCode = statusCode
    }
}

public protocol PortableHTTPTransport: Sendable {
    func response(for request: URLRequest) async throws -> PortableHTTPResponse
}

public struct PortableURLSessionTransport: PortableHTTPTransport {
    public init() {}

    public func response(for request: URLRequest) async throws -> PortableHTTPResponse {
        let (data, response) = try await URLSession.shared.data(for: request)
        guard let response = response as? HTTPURLResponse else {
            throw PortableTransportError.invalidResponse
        }
        return PortableHTTPResponse(data: data, statusCode: response.statusCode)
    }
}

enum PortableTransportError: Error {
    case invalidResponse
}

public enum PortableProviderError: LocalizedError, Sendable {
    case unauthorized(provider: PortableProvider)
    case invalidResponse(provider: PortableProvider)
    case server(provider: PortableProvider, statusCode: Int)
    case network(provider: PortableProvider, details: String)

    public var errorDescription: String? {
        switch self {
        case let .unauthorized(provider):
            "\(provider.displayName) rejected the login. Run `\(provider.rawValue)` to authenticate again."
        case let .invalidResponse(provider):
            "\(provider.displayName) returned an invalid usage response."
        case let .server(provider, statusCode):
            "\(provider.displayName) returned HTTP \(statusCode)."
        case let .network(provider, details):
            "\(provider.displayName) network request failed: \(details)"
        }
    }
}
