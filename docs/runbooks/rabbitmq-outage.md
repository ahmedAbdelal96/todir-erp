# RabbitMQ outage

Keep database transactions available for work whose invariant is synchronous, alert on publish failures and outbox age, and preserve pending outbox rows. Restore the broker or fail over the approved private cluster, verify credentials/vhost/TLS, restart the worker, and confirm publisher confirms plus consumer acknowledgements. Do not manually publish or delete outbox rows.
