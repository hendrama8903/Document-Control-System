using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using DMS.Common.Models;
using DMS.Models;
using Microsoft.Extensions.Options;
using System.Net;

namespace DMS.Services
{
    public class AWSS3Services
    {
        private readonly AWSConfiguration Config;
        private readonly IAmazonS3 AWSClient;

        public AWSS3Services(IOptions<AWSConfiguration> options)
        {
            Config = options.Value;
            AWSClient = new AmazonS3Client(Config.accessKey, Config.secretKey, RegionEndpoint.GetBySystemName(Config.region));
        }

        public async Task<DBResult> UploadFile(IFormFile file, string fileName, string folder)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    file.CopyTo(memoryStream);

                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        BucketName = Config.bucketName,
                        Key = folder + fileName,
                        InputStream = memoryStream,
                        ContentType = file.ContentType,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    var fileTransferUtility = new TransferUtility(AWSClient);
                    await fileTransferUtility.UploadAsync(uploadRequest);
                }

                return new DBResult(true, "File Uploaded Successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
                return new DBResult(false, ex.Message);
            }
        }

        public async Task<byte[]> DownloadFile(string filePath)
        {
            MemoryStream ms = null;

            try
            {
                GetObjectRequest getObjectRequest = new GetObjectRequest
                {
                    BucketName = Config.bucketName,
                    Key = filePath
                };

                using (var response = await AWSClient.GetObjectAsync(getObjectRequest))
                {
                    if (response.HttpStatusCode == HttpStatusCode.OK)
                    {
                        using (ms = new MemoryStream())
                        {
                            await response.ResponseStream.CopyToAsync(ms);
                        }
                    }
                }

                if (ms is null || ms.ToArray().Length < 1)
                    throw new FileNotFoundException(string.Format("File '{0}' is not found", filePath));

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
                throw;
            }
        }

        public async Task<DBResult> DeleteFile(string filePath)
        {
            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = Config.bucketName,
                Key = filePath
            };

            if (IsFileExists(filePath))
            {
                await AWSClient.DeleteObjectAsync(request);

                return new DBResult(true, "File Deleted Successfully");
            }
            else
            {
                return new DBResult(false, string.Format("File '{0}' is not found", filePath));
            }
        }

        public bool IsFileExists(string filePath)
        {
            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest()
                {
                    BucketName = Config.bucketName,
                    Key = filePath
                };

                var response = AWSClient.GetObjectMetadataAsync(request).Result;

                return true;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException is AmazonS3Exception awsEx)
                {
                    if (string.Equals(awsEx.ErrorCode, "NoSuchBucket"))
                        return false;

                    else if (string.Equals(awsEx.ErrorCode, "NotFound"))
                        return false;
                }

                throw;
            }
        }
    }
}
