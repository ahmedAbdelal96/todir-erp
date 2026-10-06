# Notifications

Domain modules emit notification requests; they never call SMTP or messaging vendors. In-app and email are the initial channels, with versioned, localized templates and safe variables. Delivery attempts are durable, retryable, observable, and tenant-scoped.
