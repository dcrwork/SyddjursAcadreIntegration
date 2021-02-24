using System;
using System.Linq;

namespace AcadrePWS
{
    public static class CaseManagement
    {
        /* Call this before calling any other method */
        public static void ActingFor(
            /* A short text description of the system making these calls */
            string user)
        {
            AcadreLib.Acadre.PWSHeaderExtension.User = user;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="personNameForAddressingName"></param>
        /// <param name="personCivilRegistrationNumber"></param>
        /// <param name="caseFileTypeCode"></param>
        /// <param name="accessCode"></param>
        /// <param name="caseFileTitleText"></param>
        /// <param name="journalizingCode"></param>
        /// <param name="facet"></param>
        /// <param name="caseResponsible"></param>
        /// <param name="administrativeUnit"></param>
        /// <param name="caseContent"></param>
        /// <param name="caseFileDisposalCode"></param>
        /// <param name="deletionCode"></param>
        /// <param name="caseRestrictedFromPublicText"></param>
        /// <returns></returns>
        
        public static string CloseCase(
            /* Case identifier returned by CreateCase */
            string caseId)
        {
            var caseService = Acadre.AcadreServiceFactory.GetCaseService7();

            var acadreCase = caseService.GetCase(caseId);
            acadreCase.CaseFileStatusCode = "A";
            acadreCase.ClosedDate = DateTime.Now;
            return caseService.UpdateCase(acadreCase);
        }
        public static string GetCaseURL(
            /* Case identifier returned by CreateCase */
            string caseId)
        {
            return Config.AcadreFrontEndBaseURL + "/Case/Details?caseId=" + caseId;
        }
        public static string GetCaseNumber(
            /* Case identifier returned by CreateCase */
            string caseId)
        {
            var caseService = Acadre.AcadreServiceFactory.GetCaseService7();

            var acadreCase = caseService.GetCase(caseId);
            if (acadreCase != null)
            {
                return acadreCase.CaseFileNumberIdentifier;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Create a document in acadre 7
        /// </summary>
        /// <param name="documentCaseId"></param>
        /// <param name="recordStatusCode"></param>
        /// <param name="documentTypeCode"></param>
        /// <param name="documentDescriptionText"></param>
        /// <param name="documentAccessCode"></param>
        /// <param name="documentStatusCode"></param>
        /// <param name="documentTitleText"></param>
        /// <param name="documentCategoryCode"></param>
        /// <param name="recordPublicIndicator"></param>
        /// <param name="fileName"></param>
        /// <param name="fileBytes"></param>
        /// <returns></returns>
        public static string CreateDocumentService(
            string documentCaseId,
            string recordStatusCode,
            string documentTypeCode,
            string documentDescriptionText,
            string documentAccessCode,
            string documentStatusCode,
            string documentTitleText,
            string documentCategoryCode,
            string recordPublicIndicator,
            string fileName,
            byte[] fileBytes
            )
        {
            var doc = new AcadreLib.AcadreServiceV7.CreateMainDocumentRequestType2();

            //Record Type
            var record = new AcadreLib.AcadreServiceV7.RecordType2();
            doc.Record = record;
            record.CaseFileReference = documentCaseId;
            record.RecordStatusCode = recordStatusCode;
            record.DocumentTypeCode = documentTypeCode;
            record.RecordSerialNumber = "1";
            record.AccessCode = documentAccessCode;
            record.DescriptionText = documentDescriptionText;
            record.RecordPaperStorageIndicator = false;
            record.PublicationIndicator = recordPublicIndicator;

            //Document type
            var docType = new AcadreLib.AcadreServiceV7.DocumentType();
            doc.Document = docType;
            docType.DocumentStatusCode = documentStatusCode;
            docType.DocumentCategoryCode = documentCategoryCode;
            docType.DocumentTitleText = documentTitleText;

            //Document link type
            var docLinkType = new AcadreLib.AcadreServiceV7.DocumentLinkType();
            doc.DocumentLink = docLinkType;
            doc.FileName = fileName;
            doc.XMLBinary = fileBytes;

            var documentService = Acadre.AcadreServiceFactory.GetMainDocumentService7();
            var documentId = documentService.CreateMainDocument(doc);
            return documentId;
        }

        /// <summary>
        /// Create Memo
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="accessCode"></param>
        /// <param name="caseFileReference"></param>
        /// <param name="titleText"></param>
        /// <param name="creatorReference"></param>
        /// <param name="memoTypeReference"></param>
        /// <param name="isLocked"></param>
        /// <param name="fileBytes"></param>
        /// <returns></returns>
        public static string CreateMemo(
            string fileName,
            string accessCode,
            string caseFileReference,
            string titleText,
            string creatorReference,
            string memoTypeReference,
            bool isLocked,
            byte[] fileBytes,
            DateTime eventDate
            )
        {

            var configurationService = Acadre.AcadreServiceFactory.GetConfigurationService7();
            var now = DateTime.Now;

            var memoType = new AcadreLib.AcadreServiceV7.TimedJournalMemoType();
            memoType.AccessCode = accessCode;
            memoType.CaseFileReference = caseFileReference;
            memoType.CreationDate = now;

            var userList = configurationService.GetUserList(new AcadreLib.AcadreServiceV7.EmptyRequestType()).ToList();
            var user = userList.SingleOrDefault(ut => ut.Initials == creatorReference);
            if (user != null)
            {
                memoType.CreatorReference = user.Id;
            }

            memoType.MemoTypeReference = memoTypeReference;
            memoType.IsLocked = isLocked;
            memoType.MemoEventDate = eventDate;
            memoType.MemoTitleText = titleText;

            var tempMemo = new AcadreLib.AcadreServiceV7.CreateTimedJournalMemoRequestType();
            tempMemo.FileName = fileName;
            tempMemo.XMLBinary = fileBytes;
            tempMemo.TimedJournalMemo = memoType;

            var memoService = Acadre.AcadreServiceFactory.GetMemoService7();
            var memoId = memoService.CreateMemo(tempMemo);
            return memoId;
        }
    }
}
