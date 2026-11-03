using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Core.Identity;
using Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Contracts.Platform;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrDocumentDownloadIntegrationTests : DatabaseIntegrationTestBase
{
    [HrFilesPostgresFact]
    public async Task SensitiveDocumentRequiresSensitivePermissionAndReturnsPrivateShortLivedDownload()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var endpoint = RequiredSetting("TODIR_FILES_PUBLIC_ENDPOINT");
        var accessKey = RequiredSetting("TODIR_FILES_ACCESS_KEY");
        var secretKey = RequiredSetting("TODIR_FILES_SECRET_KEY");
        var previousBucket = Environment.GetEnvironmentVariable("TODIR_FILES_BUCKET");
        var suffix = Guid.CreateVersion7().ToString("N");
        var bucket = $"hr-proof-{suffix[..16]}";
        var password = $"HrFileProof-{suffix[..8]}1!";
        var readerEmail = $"hr-file-reader-{suffix}@atlas.com";
        var privacyEmail = $"hr-file-privacy-{suffix}@atlas.com";
        var content = System.Text.Encoding.UTF8.GetBytes("private HR document acceptance proof");
        Guid tenantId;
        Guid legalEntityId;
        Guid employmentId;
        Guid otherLegalEntityId;
        Guid employeeId;
        Guid actorId;
        Guid documentId = Guid.CreateVersion7();
        Guid fileRef = Guid.CreateVersion7();
        Guid uploadedFileRef = Guid.Empty;
        string objectKey = string.Empty;
        string uploadedObjectKey = string.Empty;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select tenant.id,employment.legal_entity_id,employee.id,identity.id,employment.id from platform.tenant tenant join business_hr.employee employee on employee.tenant_id=tenant.id join business_hr.employment employment on employment.tenant_id=employee.tenant_id and employment.employee_id=employee.id and employment.status='Active' join platform.identity identity on identity.tenant_id=tenant.id and identity.normalized_email='ADMIN@ATLAS.COM' where tenant.slug='atlas-trading' and not exists(select 1 from business_hr.employment other where other.tenant_id=employee.tenant_id and other.employee_id=employee.id and other.legal_entity_id<>employment.legal_entity_id and other.status in ('Active','Suspended','NoticePeriod')) order by employment.employment_number limit 1", admin, transaction))
            await using (var reader = await scope.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow))
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
                employeeId = reader.GetGuid(2);
                actorId = reader.GetGuid(3);
                employmentId = reader.GetGuid(4);
            }

            await using (var otherEntity = new NpgsqlCommand("select entity.id from platform.legal_entity entity where entity.tenant_id=$1 and entity.id<>$2 and not exists(select 1 from business_hr.employment employment where employment.tenant_id=entity.tenant_id and employment.employee_id=$3 and employment.legal_entity_id=entity.id and employment.status in ('Active','Suspended','NoticePeriod')) order by entity.id limit 1", admin, transaction))
            {
                otherEntity.Parameters.AddWithValue(tenantId); otherEntity.Parameters.AddWithValue(legalEntityId); otherEntity.Parameters.AddWithValue(employeeId);
                otherLegalEntityId = (Guid)(await otherEntity.ExecuteScalarAsync() ?? throw new InvalidOperationException("The Atlas development seed must provide a second Legal Entity for cross-entity document testing."));
            }
            await using (var otherEmployment = new NpgsqlCommand("insert into business_hr.employment(id,tenant_id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,status_reason_code,created_by,created_at,version) values($1,$2,$3,$4,$5,'FullTime','Active',current_date,null,'document_scope_test',$6,now(),1)", admin, transaction))
            {
                otherEmployment.Parameters.AddWithValue(Guid.CreateVersion7()); otherEmployment.Parameters.AddWithValue(tenantId); otherEmployment.Parameters.AddWithValue(employeeId); otherEmployment.Parameters.AddWithValue(otherLegalEntityId); otherEmployment.Parameters.AddWithValue($"HRDOC-{suffix[..12]}"); otherEmployment.Parameters.AddWithValue(actorId);
                await otherEmployment.ExecuteNonQueryAsync();
            }

            await GrantPermissionAsync(admin, transaction, tenantId, "hr_officer", "hr.employee.document.read");
            await GrantPermissionAsync(admin, transaction, tenantId, "hr_privacy", "hr.employee.document.read");
            await GrantPermissionAsync(admin, transaction, tenantId, "hr_privacy", "hr.employee.document.read_sensitive");
            await GrantPermissionAsync(admin, transaction, tenantId, "hr_privacy", "hr.employee.document.manage");
            await GrantPermissionAsync(admin, transaction, tenantId, "hr_privacy", "hr.employee.document.verify");
            await CreateAccountAsync(admin, transaction, tenantId, readerEmail, password, "hr_officer");
            await CreateAccountAsync(admin, transaction, tenantId, privacyEmail, password, "hr_privacy");

            objectKey = $"tenant/{tenantId:D}/hr/employee/{employeeId:D}/{fileRef:D}";
            await using var insertFile = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'hr','employee',$3,$4,'medical-note.txt','text/plain',$5,$6,'Ready','sensitive',$7,now())", admin, transaction);
            insertFile.Parameters.AddWithValue(fileRef);
            insertFile.Parameters.AddWithValue(tenantId);
            insertFile.Parameters.AddWithValue(employeeId);
            insertFile.Parameters.AddWithValue(objectKey);
            insertFile.Parameters.AddWithValue((long)content.Length);
            insertFile.Parameters.AddWithValue(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant());
            insertFile.Parameters.AddWithValue(actorId);
            await insertFile.ExecuteNonQueryAsync();

            await using var insertDocument = new NpgsqlCommand("insert into business_hr.employee_document(id,tenant_id,employee_id,employment_id,legal_entity_id,scope_status,category,sensitivity,file_ref,sha256,content_type,size_bytes,verification_status,status,created_by,created_at,version) values($1,$2,$3,$4,$5,'Resolved','MedicalEvidence','Medical',$6,$7,'text/plain',$8,'Verified','Active',$9,now(),1)", admin, transaction);
            insertDocument.Parameters.AddWithValue(documentId);
            insertDocument.Parameters.AddWithValue(tenantId);
            insertDocument.Parameters.AddWithValue(employeeId);
            insertDocument.Parameters.AddWithValue(employmentId);
            insertDocument.Parameters.AddWithValue(legalEntityId);
            insertDocument.Parameters.AddWithValue(fileRef);
            insertDocument.Parameters.AddWithValue(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant());
            insertDocument.Parameters.AddWithValue((long)content.Length);
            insertDocument.Parameters.AddWithValue(actorId);
            await insertDocument.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        var s3Config = new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" };
        using var s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
        using var signedUrlClient = new HttpClient();
        var bucketCreated = false;
        var objectCreated = false;
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
            bucketCreated = true;
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = objectKey, InputStream = new MemoryStream(content), ContentType = "text/plain" });
            objectCreated = true;

            Environment.SetEnvironmentVariable("TODIR_FILES_BUCKET", bucket);
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .ConfigureTestServices(services => services.AddSingleton<IPlatformFileMalwareScanner>(provider =>
                    new FailOnceThenClamAvScanner(provider.GetRequiredService<IConfiguration>()))));

            using var readerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(readerClient, readerEmail, password, tenantId);
            using var denied = await readerClient.GetAsync(DownloadRoute(employeeId, documentId, legalEntityId));
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.DoesNotContain("X-Amz-Signature", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            using var privacyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(privacyClient, privacyEmail, password, tenantId);

            using var crossEntityList = await privacyClient.GetAsync($"/api/v1/hr/employees/{employeeId:D}/documents?legalEntityId={otherLegalEntityId:D}");
            Assert.Equal(HttpStatusCode.OK, crossEntityList.StatusCode);
            Assert.DoesNotContain((await crossEntityList.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(), row => row.GetProperty("id").GetGuid() == documentId);
            using var crossEntityDownload = await privacyClient.GetAsync(DownloadRoute(employeeId, documentId, otherLegalEntityId));
            Assert.Equal(HttpStatusCode.NotFound, crossEntityDownload.StatusCode);
            using var crossEntityVerification = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/{documentId:D}/verify?legalEntityId={otherLegalEntityId:D}", new { expectedVersion = 1, verified = true, reason = "Cross-entity isolation proof" });
            Assert.Equal(HttpStatusCode.Conflict, crossEntityVerification.StatusCode);
            using var crossEntityUpload = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/upload-intents", new
            {
                legalEntityId = otherLegalEntityId,
                employmentId,
                fileName = "cross-entity.txt",
                contentType = "text/plain",
                sizeBytes = 20,
                sensitivity = "Normal"
            });
            Assert.Equal(HttpStatusCode.BadRequest, crossEntityUpload.StatusCode);

            var uploadContent = System.Text.Encoding.UTF8.GetBytes("uploaded and scanned HR document proof");
            using var uploadIntentResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/upload-intents", new
            {
                legalEntityId,
                employmentId,
                fileName = "upload-proof.txt",
                contentType = "text/plain",
                sizeBytes = uploadContent.Length,
                sensitivity = "Normal"
            });
            Assert.True(uploadIntentResponse.StatusCode == HttpStatusCode.Created, await uploadIntentResponse.Content.ReadAsStringAsync());
            var uploadIntent = await uploadIntentResponse.Content.ReadFromJsonAsync<JsonElement>();
            var uploadUrl = uploadIntent.GetProperty("url").GetString()!;
            uploadedFileRef = uploadIntent.GetProperty("fileRef").GetGuid();
            uploadedObjectKey = $"tenant/{tenantId:D}/hr/employee/{employeeId:D}/{uploadedFileRef:D}";
            using (var multipart = new MultipartFormDataContent())
            {
                foreach (var field in uploadIntent.GetProperty("fields").EnumerateObject()) multipart.Add(new StringContent(field.Value.GetString()!), field.Name);
                multipart.Add(new ByteArrayContent(uploadContent), "file", "upload-proof.txt");
                using var uploadResponse = await signedUrlClient.PostAsync(uploadUrl, multipart);
                Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
            }
            using var completeResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/uploads/{uploadedFileRef:D}/complete", new { legalEntityId, employmentId });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, completeResponse.StatusCode);
            using var retryResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/uploads/{uploadedFileRef:D}/complete", new { legalEntityId, employmentId });
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
            Assert.True((await retryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ready").GetBoolean());
            using var createDocumentResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents", new
            {
                legalEntityId,
                employmentId,
                category = "EmploymentEvidence",
                fileRef = uploadedFileRef,
                sensitivity = "Normal"
            });
            Assert.Equal(HttpStatusCode.Created, createDocumentResponse.StatusCode);
            var uploadedDocument = await createDocumentResponse.Content.ReadFromJsonAsync<JsonElement>();
            var uploadedDocumentId = uploadedDocument.GetProperty("id").GetGuid();
            using var verifyResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/{uploadedDocumentId:D}/verify?legalEntityId={legalEntityId:D}", new { expectedVersion = 1, verified = true, reason = "Scanned document accepted" });
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            using var uploadedDownloadResponse = await privacyClient.GetAsync(DownloadRoute(employeeId, uploadedDocumentId, legalEntityId));
            Assert.Equal(HttpStatusCode.OK, uploadedDownloadResponse.StatusCode);
            var uploadedDownload = await uploadedDownloadResponse.Content.ReadFromJsonAsync<JsonElement>();
            using var uploadedBytesResponse = await signedUrlClient.GetAsync(uploadedDownload.GetProperty("url").GetString());
            Assert.Equal(HttpStatusCode.OK, uploadedBytesResponse.StatusCode);
            Assert.Equal(uploadContent, await uploadedBytesResponse.Content.ReadAsByteArrayAsync());

            var eicarContent = System.Text.Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
            using var infectedIntentResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/upload-intents", new
            {
                legalEntityId,
                fileName = "unsafe.txt",
                contentType = "text/plain",
                sizeBytes = eicarContent.Length,
                sensitivity = "Normal"
            });
            Assert.Equal(HttpStatusCode.Created, infectedIntentResponse.StatusCode);
            var infectedIntent = await infectedIntentResponse.Content.ReadFromJsonAsync<JsonElement>();
            using (var infectedForm = new MultipartFormDataContent())
            {
                foreach (var field in infectedIntent.GetProperty("fields").EnumerateObject()) infectedForm.Add(new StringContent(field.Value.GetString()!), field.Name);
                infectedForm.Add(new ByteArrayContent(eicarContent), "file", "unsafe.txt");
                using var uploadResponse = await signedUrlClient.PostAsync(infectedIntent.GetProperty("url").GetString(), infectedForm);
                Assert.True(uploadResponse.IsSuccessStatusCode, await uploadResponse.Content.ReadAsStringAsync());
            }
            var infectedFileRef = infectedIntent.GetProperty("fileRef").GetGuid();
            using var infectedCompletionResponse = await PostJsonWithCsrfAsync(privacyClient, $"/api/v1/hr/employees/{employeeId:D}/documents/uploads/{infectedFileRef:D}/complete", new { legalEntityId });
            Assert.Equal(HttpStatusCode.OK, infectedCompletionResponse.StatusCode);
            var infectedCompletion = await infectedCompletionResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(infectedCompletion.GetProperty("rejected").GetBoolean());
            Assert.Equal("files.malware_detected", infectedCompletion.GetProperty("reasonCode").GetString());

            using var allowed = await privacyClient.GetAsync(DownloadRoute(employeeId, documentId, legalEntityId));
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            var download = await allowed.Content.ReadFromJsonAsync<JsonElement>();
            var url = download.GetProperty("url").GetString();
            Assert.False(string.IsNullOrWhiteSpace(url));
            Assert.Contains("X-Amz-Signature=", url, StringComparison.Ordinal);
            var expiresAt = download.GetProperty("expiresAt").GetDateTimeOffset();
            Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddMinutes(4), DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(2));

            using var browser = new HttpClient();
            using var file = await browser.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal(content, await file.Content.ReadAsByteArrayAsync());

            await using var auditConnection = new NpgsqlConnection(adminConnectionString);
            await auditConnection.OpenAsync();
            await using var audit = new NpgsqlCommand("select after_json::text from platform.audit_entry where tenant_id=$1 and module='hr' and action='hr.employee_document.download_url_issued' and entity_id=$2 order by occurred_at desc limit 1", auditConnection);
            audit.Parameters.AddWithValue(tenantId);
            audit.Parameters.AddWithValue(documentId);
            var auditJson = (string?)await audit.ExecuteScalarAsync();
            Assert.NotNull(auditJson);
            Assert.DoesNotContain("X-Amz-Signature", auditJson, StringComparison.Ordinal);
            Assert.DoesNotContain(url, auditJson, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TODIR_FILES_BUCKET", previousBucket);
            if (objectCreated) await s3.DeleteObjectAsync(bucket, objectKey);
            if (!string.IsNullOrWhiteSpace(uploadedObjectKey)) await s3.DeleteObjectAsync(bucket, uploadedObjectKey);
            if (bucketCreated) await DeleteBucketContentsAndBucketAsync(s3, bucket);
        }
    }

    private static async Task DeleteBucketContentsAndBucketAsync(AmazonS3Client s3, string bucket)
    {
        string? continuationToken = null;
        do
        {
            var page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, ContinuationToken = continuationToken });
            if (page.S3Objects is { Count: > 0 })
                await s3.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = bucket,
                    Objects = page.S3Objects.Select(item => new KeyVersion { Key = item.Key }).ToList(),
                    Quiet = true
                });
            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        } while (continuationToken is not null);

        await s3.DeleteBucketAsync(bucket);
        var remainingBuckets = await s3.ListBucketsAsync();
        if (remainingBuckets.Buckets?.Any(item => item.BucketName == bucket) == true)
            throw new InvalidOperationException("The temporary HR document test bucket remained after cleanup.");
    }

    private static string DownloadRoute(Guid employeeId, Guid documentId, Guid legalEntityId) =>
        $"/api/v1/hr/employees/{employeeId:D}/documents/{documentId:D}/download?legalEntityId={legalEntityId:D}";

    private static async Task GrantPermissionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string roleCode, string permission)
    {
        await using var command = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,$3 from platform.role where tenant_id=$1 and code=$2 and is_active on conflict do nothing", connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(roleCode);
        command.Parameters.AddWithValue(permission);
        if (await command.ExecuteNonQueryAsync() != 1)
        {
            await using var exists = new NpgsqlCommand("select exists(select 1 from platform.role where tenant_id=$1 and code=$2 and is_active)", connection, transaction);
            exists.Parameters.AddWithValue(tenantId);
            exists.Parameters.AddWithValue(roleCode);
            if (!(bool)(await exists.ExecuteScalarAsync() ?? false)) throw new InvalidOperationException($"The canonical HR role {roleCode} is missing.");
        }
    }

    private static async Task CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string email, string password, string roleCode)
    {
        var identityId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR document test',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId);
            identity.Parameters.AddWithValue(tenantId);
            identity.Parameters.AddWithValue(email);
            identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email));
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]);
            identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password));
            identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($1,$2,$3,'HR document test','Active',now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId);
            membership.Parameters.AddWithValue(tenantId);
            membership.Parameters.AddWithValue(identityId);
            await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type) select $1,$2,id,'Tenant' from platform.role where tenant_id=$1 and code=$3 and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId);
        assignment.Parameters.AddWithValue(membershipId);
        assignment.Parameters.AddWithValue(roleCode);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException($"The canonical HR role {roleCode} is missing.");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The API did not issue a CSRF token.");
    }

    private static async Task<HttpResponseMessage> PostJsonWithCsrfAsync<T>(HttpClient client, string route, T body)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static string RequiredSetting(string key) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : throw new InvalidOperationException($"Set {key} for the HR document download integration proof.");

    private sealed class HrFilesPostgresFactAttribute : FactAttribute
    {
        public HrFilesPostgresFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_TEST_DATABASE"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_TEST_DATABASE=1 only for a disposable PostgreSQL database with the canonical development seed.";
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_FILES_PUBLIC_ENDPOINT"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_FILES_ACCESS_KEY"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_FILES_SECRET_KEY"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_FILES_SCANNER_HOST")))
                Skip = "Set local private object-store and ClamAV settings to run the HR document upload/download security proof.";
        }
    }

    private sealed class FailOnceThenClamAvScanner(IConfiguration configuration) : IPlatformFileMalwareScanner
    {
        private readonly ClamAvFileScanner scanner = new(configuration);
        private int attempts;

        public Task<bool> IsCleanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new PlatformFileStorageUnavailableException();
            return scanner.IsCleanAsync(content, cancellationToken);
        }
    }
}
