# Testing Guide

Unit and integration tests are located in the `tests/` directory.

## Running Tests Locally
To execute all test suites, use the .NET CLI:

```bash
dotnet test
```

Tests validate core components such as configuration loading, ACL enforcement, HMAC authentication (including clock skew scenarios), and IPC communications.
