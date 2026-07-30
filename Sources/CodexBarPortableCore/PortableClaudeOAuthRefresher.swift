import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

enum PortableClaudeOAuthRefresher {
    private static let tokenURL = URL(string: "https://platform.claude.com/v1/oauth/token")!
    // Claude Code's OAuth client identifier is public; it is not a client secret.
    private static let clientID = "9d1c250a-e61b-44d9-88ed-5944d1962f5e"

    static func refresh(
        _ credentials: PortableClaudeCredentials,
        transport: any PortableHTTPTransport,
        now: Date) async throws -> PortableClaudeCredentials
    {
        guard let refreshToken = credentials.refreshToken, !refreshToken.isEmpty else {
            throw PortableCredentialError.expired(provider: .claude)
        }

        var request = URLRequest(url: Self.tokenURL)
        request.httpMethod = "POST"
        request.timeoutInterval = 30
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        var components = URLComponents()
        components.queryItems = [
            URLQueryItem(name: "grant_type", value: "refresh_token"),
            URLQueryItem(name: "refresh_token", value: refreshToken),
            URLQueryItem(name: "client_id", value: Self.clientID),
        ]
        request.httpBody = (components.percentEncodedQuery ?? "").data(using: .utf8)

        let response: PortableHTTPResponse
        do {
            response = try await transport.response(for: request)
        } catch {
            throw PortableProviderError.network(provider: .claude, details: error.localizedDescription)
        }
        guard response.statusCode == 200 else {
            throw PortableCredentialError.expired(provider: .claude)
        }

        struct TokenResponse: Decodable {
            let accessToken: String
            let refreshToken: String?
            let expiresIn: Int

            enum CodingKeys: String, CodingKey {
                case accessToken = "access_token"
                case refreshToken = "refresh_token"
                case expiresIn = "expires_in"
            }
        }

        guard let token = try? JSONDecoder().decode(TokenResponse.self, from: response.data),
              !token.accessToken.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            throw PortableProviderError.invalidResponse(provider: .claude)
        }

        return PortableClaudeCredentials(
            accessToken: token.accessToken,
            refreshToken: token.refreshToken ?? refreshToken,
            expiresAt: now.addingTimeInterval(TimeInterval(token.expiresIn)),
            rateLimitTier: credentials.rateLimitTier,
            subscriptionType: credentials.subscriptionType)
    }
}
