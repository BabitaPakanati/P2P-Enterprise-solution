# Infrastructure

Deliberately empty for now.

AWS account structure, networking, RDS instance sizing, and Secrets Manager
configuration are being planned separately (see the **Decision log** in
[`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md)) and are not blocking backend
or frontend development, which both run against local Docker/Postgres in the
meantime.

Once that sizing work lands, this folder will hold an AWS CDK (C#) app that
provisions:

- VPC, subnets, security groups
- RDS for PostgreSQL (Multi-AZ)
- ECS Fargate services for `P2P.Api` and `P2P.Workers`
- S3 bucket for attachments
- Secrets Manager entries for connection strings and API keys
- EventBridge bus + SQS queues for the business event layer

Every resource here is one implementation of the portability interfaces described
in the architecture doc's adapter-boundary diagram - an on-prem deployment swaps
this folder for Docker Compose / customer-managed equivalents, not for a different
application build.
