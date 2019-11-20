using System;
using RestSharp;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Net.Http;
using System.Windows;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace AcadreLib
{
    public class AcadreService
    {
        private AcadreServiceV7.CaseService7 caseService;
        private AcadreServiceV7.ContactService7 contactService;
        private AcadreServiceV7.ConfigurationService7 configurationService;
        private AcadreServiceV7.MainDocumentService7 documentService;
        private AcadreServiceV7.MemoService7 memoService;
        private AcadreServiceV4.ContactService4 contactService4;
        private AcadreServiceV4.DocumentService4 documentService4;
        private AcadreServiceV4.MemoService4 memoService4;
        private AcadreServiceV4.CaseService4 caseService4;
        private CPRBrokerService CPRBrokerService;
        private AcadrePWI.Acadre.AcadrePWIservice PWIservice;
        string CaseFileTypeCode = "BUSAG";
        public AcadreService(string Acadre_baseurlPWI,string Acadre_urlPWS, string Acadre_username, string Acadre_password, string Acadre_domain, string Acadre_actingForUsername, string CPRBroker_EndpointURL, string CPRBroker_UserToken, string CPRBroker_ApplicationToken)
        {
            Acadre.PWSHeaderExtension.User = Acadre_actingForUsername;
            System.Net.NetworkCredential networkCredential = new System.Net.NetworkCredential(Acadre_username, Acadre_password, Acadre_domain);
            caseService = new AcadreServiceV7.CaseService7
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS
            };
            contactService = new AcadreServiceV7.ContactService7
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS
            };
            configurationService = new AcadreServiceV7.ConfigurationService7
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS
            };
            documentService = new AcadreServiceV7.MainDocumentService7
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS
            };
            memoService = new AcadreServiceV7.MemoService7
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS
            };
            documentService4 = new AcadreServiceV4.DocumentService4
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS.Replace("7.asmx", "4.asmx")
            };
            memoService4 = new AcadreServiceV4.MemoService4
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS.Replace("7.asmx", "4.asmx")
            };
            caseService4 = new AcadreServiceV4.CaseService4
            {
                Credentials = networkCredential,
                Url = Acadre_urlPWS.Replace("7.asmx", "4.asmx")
            };
            CPRBrokerService = new CPRBrokerService(CPRBroker_EndpointURL, CPRBroker_UserToken, CPRBroker_ApplicationToken);
            PWIservice = new AcadrePWI.Acadre.AcadrePWIservice(Acadre_baseurlPWI, Acadre_username,Acadre_password);
        }

        public IEnumerable<ChildCase> SearchChildren(SearchCriterion searchCriterion)
        {
            // Acadre PWS metoden er begrænset af at der kun kan fremsøges 100 sager af gangen. Derfor suppleres der i disse tilfælde med Acadre PWI
            // Følgende parametre kan ikke søges efter gennem API:
            // 1. KLE
            // 2. PrimaryContactsName (men der kan godt søges efter specifik Primær kontakt ID)

            List<ChildCase> childCases = new List<ChildCase>();
            var userList = configurationService.GetUserList(new AcadreServiceV7.EmptyRequestType()).ToList(); // Herfra kan CaseManager aflæses. Listen indeholder alle brugere i Acadre som ikke er ophørte.
            var UnknownUser = new AcadreServiceV7.UserType() { Initials = "", Name = "", Id = "" };

            AcadreServiceV7.AdvancedCaseSearchRequestType3 advancedCaseSearchRequestType = new AcadreServiceV7.AdvancedCaseSearchRequestType3();

            // For at sænke risikoen for at ramme de max 100 sager, så splittes søgningen op i åbne og lukkede sager
            string[] StatusCodes;
            if (searchCriterion.IsClosed.HasValue)
            {
                if (searchCriterion.IsClosed.Value)
                    StatusCodes = new string[] { "A" };
                else
                    StatusCodes = new string[] { "B" };
            }
            else
            {
                StatusCodes = new string[] { "A", "B" }; //StatusCodes = new string[] { "A", "B", "P", "S" };
            }
            // Sagstype er det samme for alle børnesager
            advancedCaseSearchRequestType.TypeCode = CaseFileTypeCode;

            // Afdeling er obligatorisk søgekriterie
            if (searchCriterion.AcadreOrgID != 0)
            {
                advancedCaseSearchRequestType.AdministrativeUnitId = searchCriterion.AcadreOrgID.ToString();
            }

            // Sagsansvarlig er valgfrit søgekriterie           
            if (searchCriterion.CaseManagerInitials != null)
            {
                var user = userList.SingleOrDefault(ut => ut.Initials == searchCriterion.CaseManagerInitials);
                if (user != null)
                {
                    advancedCaseSearchRequestType.ResponsibleUserId = user.Id;
                }
                else
                    return childCases;
            }

            // Der kan desværre ikke søges efter kontakter med navn fordi Acadre PWS ikke kan returnere mere end 100 personer.
            string[] foundContactsGUI = new string[] { null };
            if (searchCriterion.PrimaryContactsName != "" && searchCriterion.ChildCPR == "*")
            {
                var searchContactCriterion = new AcadreServiceV7.SearchContactCriterionType2();
                //AcadreServiceV7.ContactType contactType = new AcadreServiceV7.ContactType();
                searchContactCriterion.ContactTypeName = "Person";
                searchContactCriterion.SearchTerm = searchCriterion.PrimaryContactsName;
                foundContactsGUI = contactService.SearchContacts(searchContactCriterion).Select(x=>x.GUID).ToArray();
                if (foundContactsGUI.Length == 0)
                {
                    return childCases;
                }
                else if (foundContactsGUI.Length > 10)
                {
                    // Brug Acadre PWI i stedet
                    var AcadreCases = PWIservice.SearchCases(searchCriterion);
                    // Add to list
                    foreach (var AcadreCase in AcadreCases)
                    {
                        var user = userList.SingleOrDefault(ut => ut.Id == AcadreCase.ResponsibleUserId.ToString()) ?? UnknownUser;

                        childCases.Add(new ChildCase()
                        {
                            CaseID = AcadreCase.Id,
                            ChildName = AcadreCase.Description,
                            ChildCPR = AcadreCase.Title,
                            CaseManagerInitials = user.Initials,
                            CaseManagerName = user.Name,
                            CaseContent = AcadreCase.Content,
                            IsClosed = AcadreCase.Status.IsClosed,
                            Note = "",
                            CaseNumberIdentifier = AcadreCase.Year.ToString().Substring(2, 2) + "/" + AcadreCase.SequenceNumber
                        });
                    }
                    return childCases;
                }
            }

            // CPR er valgfrit søgekriterie
            advancedCaseSearchRequestType.Title = searchCriterion.ChildCPR;

            
            var foundCases = new List<AcadreServiceV7.CaseFileType3>();
            foreach (var PrimaryContactGUI in foundContactsGUI)
            {
                if (PrimaryContactGUI != null)
                    advancedCaseSearchRequestType.CustomFields = new AcadreServiceV7.CustomField[] {
                                                                                                new AcadreServiceV7.CustomField {Value = searchCriterion.CaseContent, Name = "df1"},
                                                                                                new AcadreServiceV7.CustomField {Value = PrimaryContactGUI, Name = "df25"}
                                                                                               };
                else
                    advancedCaseSearchRequestType.CustomFields = new AcadreServiceV7.CustomField[] {
                                                                                                new AcadreServiceV7.CustomField {Value = searchCriterion.CaseContent, Name = "df1"}
                                                                                               };
                foreach (var StatusCode in StatusCodes)
                {
                    advancedCaseSearchRequestType.StatusCode = StatusCode;
                    var result = caseService.AdvancedCaseSearch(advancedCaseSearchRequestType);
                    if(result.Length == 100)
                    {
                        // Brug Acadre PWI i stedet
                        var AcadreCases = PWIservice.SearchCases(searchCriterion);
                        // Add to list
                        foreach(var AcadreCase in AcadreCases)
                        {
                            var user = userList.SingleOrDefault(ut => ut.Id == AcadreCase.ResponsibleUserId.ToString()) ?? UnknownUser;

                            childCases.Add(new ChildCase()
                            {
                                CaseID = AcadreCase.Id,
                                ChildName = AcadreCase.Description,
                                ChildCPR = AcadreCase.Title,
                                CaseManagerInitials = user.Initials,
                                CaseManagerName = user.Name,
                                CaseContent = AcadreCase.Content,
                                IsClosed = AcadreCase.Status.IsClosed,
                                Note = "",
                                CaseNumberIdentifier = AcadreCase.Year.ToString().Substring(2,2) + "/" + AcadreCase.SequenceNumber
                            });
                        }
                        return childCases;
                    }
                    foundCases.AddRange(result);
                }
            }


            if (!(searchCriterion.KLE == null))
            {
                foundCases.RemoveAll(x => !x.Classification.Category.Where(y => y.Equals("KL Koder")).Select(y => y.Literal).Contains(searchCriterion.KLE));
            }

            
            foreach (AcadreServiceV7.BUCaseFileType foundCase in foundCases)
            {
                var user = userList.SingleOrDefault(ut => ut.Id == foundCase.CaseFileManagerReference) ?? UnknownUser;

                childCases.Add(new ChildCase()
                {
                    CaseID = int.Parse(foundCase.CaseFileIdentifier),
                    ChildName = foundCase.TitleAlternativeText,
                    ChildCPR = foundCase.CaseFileTitleText,
                    CaseManagerInitials = user.Initials,
                    CaseManagerName = user.Name,
                    CaseContent = foundCase.CustomFields.df1,
                    IsClosed = foundCase.CaseFileStatusCode == "A",
                    Note = foundCase.Note,
                    CaseNumberIdentifier = foundCase.CaseFileNumberIdentifier
                });
            }

            return childCases;
        }
        public Child GetChildInfo(string CPR)
        {
            int CaseID = 0;

            SearchCriterion searchCriterion = new SearchCriterion()
            {
                ChildCPR = CPR,
                CaseContent = "Løbende journal*"
            };
            IEnumerable<ChildCase> childCases = SearchChildren(searchCriterion);
            foreach (var childCase in childCases)
            {
                CaseID = childCase.CaseID;
                if (!childCase.IsClosed) // Hvis sagen ikke er afsluttet så behøver vi ikke at gå resten igennem
                    break;
            }
            if (CaseID != 0)
                return GetChildInfo(CaseID);
            else
                return CPRBrokerService.GetChild(CPR);
        }
        public Child GetChildInfo(int CaseID)
        {
            string CPR;
            Child child = new Child();
            AcadreServiceV7.BUCaseFileType Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());
            var user = GetUser(Case.CaseFileManagerReference);
            child = CPRBrokerService.GetChild(Case.CaseFileTitleText);

            child.Note = Case.Note;
            child.CaseID = CaseID;
            child.CaseNumberIdentifier = Case.CaseFileNumberIdentifier;
            child.CaseManagerInitials = user.Initials;
            child.CaseManagerName = user.Name;
            child.CaseIsClosed = Case.CaseFileStatusCode == "A";

            if (child.Siblings == null)
                return child;

            var siblings = child.Siblings.ToArray();
            for (int i = 0; i < child.Siblings.Count(); i++)
            {
                CPR = siblings[i].SimpleChild.CPR;
                SearchCriterion searchCriterion = new SearchCriterion()
                {
                    ChildCPR = CPR,
                    CaseContent = "Løbende journal*"
                };
                IEnumerable<ChildCase> childCases = SearchChildren(searchCriterion);

                foreach (var childCase in childCases)
                {
                    siblings[i].CaseID = childCase.CaseID;
                    siblings[i].Note = childCase.Note;
                    siblings[i].CaseNumberIdentifier = childCase.CaseNumberIdentifier;
                    siblings[i].CaseManagerInitials = childCase.CaseManagerInitials;
                    siblings[i].CaseManagerName = childCase.CaseManagerName;
                    siblings[i].CaseIsClosed = childCase.IsClosed;
                    if (!childCase.IsClosed) // Hvis sagen ikke er afsluttet så behøver vi ikke at gå resten igennem
                        break;
                }
            }
            child.Siblings = siblings.AsEnumerable();
            return child;
        }
        public int CreateChildJournal(string CPR, int AcadreOrgID, string CaseManagerInitials)
        {
            CPR = CPR.Replace("-", "").Trim();
            if (!CPRBrokerService.IsValidCPR(CPR)) throw new Exception("CPR-nummeret (" + CPR + ") er ikke gyldigt.");

            string KLE = "27.24.00";
            string CaseContent = "Løbende journal";
            string PublicationRestriction = "2"; // Aktindsigt, 2 = Delvis
            string CaseStatus = "B"; // Sagsstatus, B = Under Behandling
            string CaseDisposalCode = "B"; // Kassationskode, B = Bevares
            string DeletionCode = "P1825D"; // Slettekode
            string AccessCode = "BN"; // Adgangskode, BN = Børnesager
            string Classification = "G01"; // Facet
            string SubType = "Person"; // Undertype
            int SpecialistID = 8;
            int RecommendationID = 1;
            int CategoryID = 4;

            // Find informationer om barnet og eventuelle eksisterende sager
            var childinfo = GetChildInfo(CPR);

            // Hvis der blev fundet en åben sag så returneres denne sags CaseID. Hvis der kun blev fundet en lukket sag så returneres -1.
            if (childinfo.CaseID != 0 && !caseService.Url.Contains("esdhtest2"))
            {
                if (!childinfo.CaseIsClosed)
                    return childinfo.CaseID;
                else
                    return -1;
            }

            // look up contact by cpr number
            var PrimaryContact = GetCreateAcadreContact(CPR);

            // Forældre skal også med som kontakter
            var parents = new List<SimplePerson>();
            parents.AddRange(childinfo.Mom ?? new SimplePerson[] { }); parents.AddRange(childinfo.Dad ?? new SimplePerson[] { });
            var parentsGUI = new List<string>();
            foreach (var parent in parents)
            {
                parentsGUI.Add(GetCreateAcadreContact(parent.CPR).GUID);
            }

            var createCaseRequest = new AcadreServiceV7.CreateCaseRequestType();
            AcadreServiceV7.CaseFileType3 caseFile;
            //AcadreServiceV7.BUCaseFileType caseFile = new AcadreServiceV7.BUCaseFileType();
            if (CaseFileTypeCode == "BUSAG")
            {
                AcadreServiceV7.BUCaseFileType BUcaseFile = new AcadreServiceV7.BUCaseFileType();
                BUcaseFile.SpecialistId = SpecialistID; // Faggruppe
                BUcaseFile.SpecialistIdSpecified = true;
                BUcaseFile.RecommendationId = RecommendationID; // Henvendelse
                BUcaseFile.RecommendationIdSpecified = true;
                BUcaseFile.CategoryId = CategoryID; // Kategori
                BUcaseFile.CategoryIdSpecified = true;
                caseFile = BUcaseFile;
            }
            else
            {
                caseFile = new AcadreServiceV7.CaseFileType3();
            }
            caseFile.CaseFileTypeCode = CaseFileTypeCode;
            caseFile.Year = DateTime.Now.Year.ToString();
            caseFile.CreationDate = DateTime.Now;
            caseFile.CaseFileTitleText = CPR;
            caseFile.TitleAlternativeText = PrimaryContact.ContactTitle;
            caseFile.RestrictedFromPublicText = PublicationRestriction;
            caseFile.CaseFileStatusCode = CaseStatus;
            caseFile.CaseFileDisposalCode = CaseDisposalCode;
            caseFile.DeletionCode = DeletionCode;
            caseFile.AccessCode = AccessCode;
            caseFile.SubType = SubType;
            caseFile.AdministrativeUnit = new AcadreServiceV7.AdministrativeUnitType[]
            {
                            new AcadreServiceV7.AdministrativeUnitType() { AdministrativeUnitReference = AcadreOrgID.ToString() }
            };

            caseFile.CustomFieldCollection = new AcadreServiceV7.CustomField[]
            {
                            new AcadreServiceV7.CustomField(){Name = "df1",Value = CaseContent}
                            ,new AcadreServiceV7.CustomField(){Name = "df25",Value = PrimaryContact.GUID} //contactGUID
            };

            caseFile.Classification = new AcadreServiceV7.ClassificationType
            {
                Category = new AcadreServiceV7.CategoryType[] {
                            new AcadreServiceV7.CategoryType(){ Principle="KL Koder", Literal = KLE }
                            ,new AcadreServiceV7.CategoryType(){ Principle="Facetter", Literal = Classification}
                        }
            };

            // Barnet tilføjes som primær part og forældre tilføjes som parter
            caseFile.Party = new AcadreServiceV7.PartyType[1 + parentsGUI.Count];
            caseFile.Party[0] = new AcadreServiceV7.PartyType()
                {
                    CreationDate = DateTime.Now
                    ,ContactReference = PrimaryContact.GUID
                    ,PublicAccessLevelReference = "3"
                    ,IsPrimary = true
                };
            int i = 1;
            foreach(var parentGUI in parentsGUI)
            {
                caseFile.Party[i] = new AcadreServiceV7.PartyType()
                {
                    CreationDate = DateTime.Now
                    ,ContactReference = parentGUI
                    ,PublicAccessLevelReference = "3"
                    ,IsPrimary = false
                };
                i++;
            }

            createCaseRequest.CaseFile = caseFile;


            var userList = configurationService.GetUserList(new AcadreServiceV7.EmptyRequestType()).ToList();
            var user = userList.SingleOrDefault(ut => ut.Initials == CaseManagerInitials);
            if (user != null)
            {
                createCaseRequest.CaseFile.CaseFileManagerReference = user.Id;
            }
            //createCaseRequest.CaseFile.
            var createCaseResponse = caseService.CreateCase(createCaseRequest);

            // check for multicase (samlesag) response.
            if (createCaseResponse.CreateCaseAndAMCResult == AcadreServiceV7.CreateCaseAndAMCResultType.CaseNotCreatedAndListAMCReceived)
            {
                // create the case in all the multicases
                createCaseRequest.MultiCaseIdentifiers = createCaseResponse.MultiCaseIdentifiers;
                createCaseResponse = createCaseResponse = caseService.CreateCase(createCaseRequest);
            }
            return int.Parse(createCaseResponse.CaseFileIdentifier);
        }
        public IEnumerable<ChildCase> GetChildCases(int CaseID)
        {
            List<ChildCase> childCases = new List<ChildCase>();
            AcadreServiceV7.BUCaseFileType Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());
            string CPR = Case.CaseFileTitleText;
            
            AcadreServiceV7.AdvancedSearchCaseCriterionType2 searchCriterion = new AcadreServiceV7.AdvancedSearchCaseCriterionType2();
            searchCriterion.CaseFileTitleText = CPR;
            searchCriterion.CaseFileTypeCode = CaseFileTypeCode;
            var userList = configurationService.GetUserList(new AcadreServiceV7.EmptyRequestType()).ToList();
            foreach (AcadreServiceV7.CaseSearchResponseType foundCase in caseService.SearchCases(searchCriterion))
            {
                Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(foundCase.CaseFileReference);
                var user = userList.SingleOrDefault(ut => ut.Id == Case.CaseFileManagerReference);
                if (user == null)
                {
                    user = new AcadreServiceV7.UserType() { Initials = "", Name = "" };
                }
                childCases.Add(new ChildCase
                {
                    CaseID = int.Parse(foundCase.CaseFileReference),
                    CaseContent = Case.CustomFields.df1,
                    ChildCPR = CPR,
                    CaseNumberIdentifier = Case.CaseFileNumberIdentifier,
                    CaseManagerInitials = user.Initials,
                    CaseManagerName = user.Name,
                    Note = Case.Note,
                    ChildName = Case.TitleAlternativeText,
                    IsClosed = Case.CaseFileStatusCode == "A"
                });
            }
            return childCases;
        }
        public IEnumerable<JournalDocument> GetChildJournalDocuments(int CaseID)
        {
            List<JournalDocument> journalDocuments = new List<JournalDocument>();
            AcadreServiceV7.BUCaseFileType Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());
            AcadreServiceV7.AdvancedSearchCaseCriterionType2 searchCriterion = new AcadreServiceV7.AdvancedSearchCaseCriterionType2();
            searchCriterion.CaseFileTitleText = Case.CaseFileTitleText;
            searchCriterion.CaseFileTypeCode = CaseFileTypeCode;
            // Henter dokumenter
            foreach (AcadreServiceV7.CaseSearchResponseType foundCase in caseService.SearchCases(searchCriterion))
            {
                journalDocuments.AddRange(GetChildCaseDocuments(int.Parse(foundCase.CaseFileReference)));
            }
            //FlowDocument document = new FlowDocument();
            foreach (var memo in memoService4.GetAllMemo(CaseID.ToString()))
            {
                if (memo == null)
                {
                    throw new Exception("Der opstod en fejl ved udtræk af journalnotater: Den kaldte metode returnerede null værdier");
                }
                //var outputbinary = documentService4.GetPhysicalDocument(new AcadreServiceV4.FileVersionReferenceType()
                //{
                //    FileReference = memo.MemoFileReference,
                //    Version = "1"
                //});
                //TextRange txtRange;
                //using (MemoryStream stream = new MemoryStream(outputbinary))
                //{
                //    // create a TextRange around the entire document
                //    txtRange = new TextRange(document.ContentStart, document.ContentEnd);
                //    txtRange.Load(stream, DataFormats.Rtf);
                //}
                journalDocuments.Add(new JournalDocument
                {
                    Type = "Memo",
                    DocumentID = int.Parse(memo.MemoIdentifier),
                    Title = memo.MemoTitleText,
                    LastChangedDate = memo.MemoEventDate,
                    CaseID = CaseID,
                    CaseNumberIdentifier = Case.CaseFileNumberIdentifier,
                    DocumentMemoDescription = "" // txtRange.Text // Her skal der bruges indholdet af notatet
                });
            }

            return journalDocuments;
        }
        public IEnumerable<JournalDocument> GetChildCaseDocuments(int CaseID)
        {
            List<JournalDocument> journalDocuments = new List<JournalDocument>();
            var Case = caseService.GetCase(CaseID.ToString());

            string caseNumberIdentifier = Case.CaseFileNumberIdentifier;
            foreach (var document in documentService.GetAllDocuments(CaseID.ToString()))
            {
                journalDocuments.Add(new JournalDocument
                {
                    Type = "Document",
                    DocumentID = int.Parse(document.Document.DocumentIdentifier),
                    Title = document.Document.DocumentTitleText,
                    LastChangedDate = document.Record.RegistrationDate,
                    CaseID = CaseID,
                    CaseNumberIdentifier = Case.CaseFileNumberIdentifier,
                    DocumentMemoDescription = document.Record.DescriptionText
                });
            }
            return journalDocuments;
        }
        public void SetBUComment(int CaseID, string NewComment)
        {
            var Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());
            Case.Note = NewComment;
            try
            {
                caseService.UpdateCase(Case);
            }
            catch (Exception e)
            {
                throw new Exception("Kunne ikke opdatere Acadre sagen", e);
            }

        }
        public JournalDocument GetMemo(int MemoID)
        {
            JournalDocument journalDocument;
            var memo = memoService4.GetMemo(MemoID.ToString());
            var outputbinary = documentService4.GetPhysicalDocument(new AcadreServiceV4.FileVersionReferenceType()
            {
                FileReference = memo.MemoFileReference,
                Version = "1"
            });
            File.WriteAllBytes("C:/Temp/test.rtf", outputbinary);
            FlowDocument document = new FlowDocument();
            TextRange txtRange = null;
            using (MemoryStream stream = new MemoryStream(outputbinary))
            {
                // create a TextRange around the entire document
                txtRange = new TextRange(document.ContentStart, document.ContentEnd);
                txtRange.Load(stream, DataFormats.Rtf);
            }
            journalDocument = new JournalDocument
            {
                Type = "Memo",
                DocumentID = MemoID,
                Title = memo.MemoTitleText,
                LastChangedDate = memo.MemoEventDate,
                CaseID = int.Parse(memo.CaseFileReference),
                CaseNumberIdentifier = "",
                DocumentMemoDescription = txtRange.Text // Her skal der bruges indholdet af notatet
            };
            return journalDocument;
        }
        public int CreateMemo(MemoRequest memoRequest)
        {
            var now = DateTime.Now;

            var memoType = new AcadreLib.AcadreServiceV7.TimedJournalMemoType();
            memoType.AccessCode = memoRequest.accessCode;
            memoType.CaseFileReference = memoRequest.caseFileReference;
            memoType.CreationDate = now;

            var userList = configurationService.GetUserList(new AcadreLib.AcadreServiceV7.EmptyRequestType()).ToList();
            var user = userList.SingleOrDefault(ut => ut.Initials == memoRequest.creatorReference);
            if (user != null)
            {
                memoType.CreatorReference = user.Id;
            }

            memoType.MemoTypeReference = memoRequest.memoTypeReference;
            memoType.IsLocked = memoRequest.isLocked;
            memoType.MemoEventDate = memoRequest.eventDate;
            memoType.MemoTitleText = memoRequest.titleText;

            var tempMemo = new AcadreLib.AcadreServiceV7.CreateTimedJournalMemoRequestType();
            tempMemo.FileName = memoRequest.fileName;
            tempMemo.XMLBinary = memoRequest.fileBytes;
            tempMemo.TimedJournalMemo = memoType;

            var memoId = int.Parse(memoService.CreateMemo(tempMemo));
            return memoId;
        }
        // Changes the Responsible CaseManager on all the childrens cases where the CaseManagerInitials = oldCaseManagerInitials. If oldCaseManagerInitials is "" then all child cases CaseManager is changed to newCaseManagerInitials.
        public void ChangeChildResponsible(string oldCaseManagerInitials, string newCaseManagerInitials, int newAcadreOrgID, int CaseID)
        {
            var userList = configurationService.GetUserList(new AcadreServiceV7.EmptyRequestType()).ToList();
            var newUser = userList.SingleOrDefault(ut => ut.Initials == newCaseManagerInitials);
            if (newUser == null)
                throw new Exception("Der fandtes ikke en bruger i Acadre med initialerne " + newCaseManagerInitials);

            AcadreServiceV7.BUCaseFileType Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());
            if (!Case.CustomFieldCollection.Single(x => x.Name == "df1").Value.Contains("Løbende "))
            {
                Case.CaseFileManagerReference = newUser.Id;
                Case.AdministrativeUnit = new AcadreServiceV7.AdministrativeUnitType[]
                {
                    new AcadreServiceV7.AdministrativeUnitType() { AdministrativeUnitReference = newAcadreOrgID.ToString()}
                };
                try
                {
                    caseService.UpdateCase(Case);
                }
                catch (Exception e)
                {
                    throw new Exception("Kunne ikke opdatere Acadre sagen", e);
                }
                return;
            }
            string CPR = Case.CaseFileTitleText;

            AcadreServiceV7.AdvancedSearchCaseCriterionType2 searchCriterion = new AcadreServiceV7.AdvancedSearchCaseCriterionType2();
            searchCriterion.CaseFileTitleText = CPR;
            searchCriterion.CaseFileTypeCode = CaseFileTypeCode;

            foreach (AcadreServiceV7.CaseSearchResponseType foundCase in caseService.SearchCases(searchCriterion))
            {
                Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(foundCase.CaseFileReference);
                var user = userList.SingleOrDefault(ut => ut.Id == Case.CaseFileManagerReference);
                if (user == null)
                    user = new AcadreServiceV7.UserType { Initials = "" };
                if (user.Initials == oldCaseManagerInitials || oldCaseManagerInitials == "" || user.Initials == "")
                {
                    Case.CaseFileManagerReference = newUser.Id;
                    Case.AdministrativeUnit = new AcadreServiceV7.AdministrativeUnitType[]
                    {
                        new AcadreServiceV7.AdministrativeUnitType() { AdministrativeUnitReference = newAcadreOrgID.ToString()}
                    };
                    try
                    {
                        caseService.UpdateCase(Case);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Kunne ikke opdatere Acadre sagen", e);
                    }
                }
            }
        }
        public void ChangeCaseContent(string newContent,int CaseID)
        {
            AcadreServiceV7.BUCaseFileType Case = (AcadreServiceV7.BUCaseFileType)caseService.GetCase(CaseID.ToString());

            for (int i = 0;i < Case.CustomFieldCollection.Count();i++)
            {
                if(Case.CustomFieldCollection[i].Name == "df1")
                {
                    Case.CustomFieldCollection[i].Value = newContent;
                    break;
                }
            }
            try
            {
                caseService.UpdateCase(Case);
            }
            catch (Exception e)
            {
                throw new Exception("Kunne ikke opdatere Acadre sagen", e);
            }
            return;
        }
        private AcadreServiceV7.UserType GetUser(string UserReference)
        {
            var userList = configurationService.GetUserList(new AcadreServiceV7.EmptyRequestType()).ToList(); // Herfra kan CaseManager aflæses
            return userList.SingleOrDefault(ut => ut.Id == UserReference);
        }

        public string CreateCase(
            /* Case party's name */
            string personNameForAddressingName,
            /* Case party's CPR number */
            string personCivilRegistrationNumber,
            /* Case type. One of the following:
			 * "EMSAG" (emnesag)
			 * "BGSAG" (borgersag)
			 * "EJSAG" (ejendomssag)
			 * "PERSAG" (personalesag)
			 * "BYGGESAG" (byggesag) 
			 * "BUSAG" (Børn og unge sag)*/
            string caseFileTypeCode,
            /* Security code. One of the following:
			 * "BO" (borgersag)
			 * "KK" (kommunekode)
			 * "LP" (lukket punkt)
			 * "PP" (personpunkt) */
            string accessCode,
            /* Case title */
            string caseFileTitleText,
            /* KLE journalizing code (http://www.kle-online.dk/emneplan/00/) */
            string journalizingCode,
            /* KLE facet for the specified journalizing code */
            string facet,
            /* Username of the user creating this case; get from Active Directory
             * (the AcadreServiceFactory.GetConfigurationService7().GetUserList(...) method will
             * return a full list) */
            string caseResponsible,
            /* Identifier of the administrative unit; should probably be "80" for "Løn og personale"
             * (the AcadreServiceFactory.GetConfigurationService7().GetAdminUnitList(...) method
             * will return a full list) */
            string administrativeUnit,
            /* Case content */
            string caseContent,
            /* Discard code. One of the following(?):
             * "B" (bevares),
             * "K" (kasseres),
             * "K5" (kasseres efter 5 år),
             * "K10" (kasseres efter 10 år)
             * "K20" (kasseres efter 20 år) */
            string caseFileDisposalCode,
            /* Deletion code; "P1800D" seems to be the standard value here */
            string deletionCode,
            string caseRestrictedFromPublicText,
            string SpecialistID,
            string RecommendationID,
            string CategoryID,
            string SubType
            )
        {

            // look up contact by cprnumber
            var searchContactCriterion = new AcadreServiceV7.SearchContactCriterionType2();
            searchContactCriterion.ContactTypeName = "Person";
            searchContactCriterion.SearchTerm = personCivilRegistrationNumber;

            AcadreServiceV7.ContactSearchResponseType[] foundContacts =
                contactService.SearchContacts(searchContactCriterion);

            var PrimaryContact = GetCreateAcadreContact(personCivilRegistrationNumber);

            // create the case
            var createCaseRequest = new AcadreServiceV7.CreateCaseRequestType();

            AcadreServiceV7.CaseFileType3 caseFile;
            if (caseFileTypeCode == "BUSAG")
            {
                AcadreServiceV7.BUCaseFileType BUcaseFile = new AcadreServiceV7.BUCaseFileType();
                try
                {
                    BUcaseFile.SpecialistId = int.Parse(SpecialistID); // Faggruppe
                    BUcaseFile.SpecialistIdSpecified = true;
                }
                catch (Exception ex)
                {
                    BUcaseFile.SpecialistIdSpecified = false;
                }
                try
                {
                    BUcaseFile.RecommendationId = int.Parse(RecommendationID); // Henvendelse
                    BUcaseFile.RecommendationIdSpecified = true;
                }
                catch (Exception ex)
                {
                    BUcaseFile.RecommendationIdSpecified = false;
                }
                try
                {
                    BUcaseFile.CategoryId = int.Parse(CategoryID); // Kategori
                    BUcaseFile.CategoryIdSpecified = true;
                }
                catch (Exception ex)
                {
                    BUcaseFile.CategoryIdSpecified = false;
                }
                caseFile = BUcaseFile;
            }
            else
            {
                caseFile = new AcadreServiceV7.CaseFileType3();
            }
            caseFile.SubType = SubType; // SubType from input argument
            caseFile.CaseFileTypeCode = caseFileTypeCode;
            caseFile.Year = DateTime.Now.Year.ToString();
            caseFile.CreationDate = DateTime.Now;
            caseFile.CaseFileTitleText = personCivilRegistrationNumber; // must be set to contact cpr number for BGSAG
            caseFile.TitleUnofficialIndicator = false;
            caseFile.TitleAlternativeText = PrimaryContact.ContactTitle; // must be set to contact name for BGSAG
            caseFile.RestrictedFromPublicText = caseRestrictedFromPublicText;
            caseFile.CaseFileStatusCode = "B";
            caseFile.CaseFileDisposalCode = caseFileDisposalCode;
            caseFile.DeletionCode = deletionCode;
            caseFile.AccessCode = accessCode;

            caseFile.AdministrativeUnit = new AcadreServiceV7.AdministrativeUnitType[]
                {
                new AcadreServiceV7.AdministrativeUnitType() { AdministrativeUnitReference=administrativeUnit }
                };

            caseFile.CustomFieldCollection = new AcadreServiceV7.CustomField[]
                {
                    new AcadreServiceV7.CustomField(){Name = "df1",Value = caseContent}
                    ,new AcadreServiceV7.CustomField(){Name = "df25",Value = PrimaryContact.GUID}
                };

            caseFile.Classification = new AcadreServiceV7.ClassificationType
            {
                Category = new AcadreServiceV7.CategoryType[] {
                    new AcadreServiceV7.CategoryType(){ Principle="KL Koder", Literal = journalizingCode }
                       ,new AcadreServiceV7.CategoryType(){ Principle="Facetter", Literal = facet }
                   }
            };

            if (caseFileTypeCode == "BUSAG")
            {
                var child = CPRBrokerService.GetChild(personCivilRegistrationNumber);
                // Forældre skal også med som kontakter
                var parents = new List<SimplePerson>();
                parents.AddRange(child.Mom ?? new SimplePerson[] { }); parents.AddRange(child.Dad ?? new SimplePerson[] { });
                var parentsGUI = new List<string>();
                foreach (var parent in parents)
                {
                    parentsGUI.Add(GetCreateAcadreContact(parent.CPR).GUID);
                }
                // Barnet tilføjes som primær part og forældre tilføjes som parter
                caseFile.Party = new AcadreServiceV7.PartyType[1 + parentsGUI.Count];
                caseFile.Party[0] = new AcadreServiceV7.PartyType()
                {
                    CreationDate = DateTime.Now,
                    ContactReference = PrimaryContact.GUID,
                    PublicAccessLevelReference = "3",
                    IsPrimary = true
                };
                int i = 1;
                foreach (var parentGUI in parentsGUI)
                {
                    caseFile.Party[i] = new AcadreServiceV7.PartyType()
                    {
                        CreationDate = DateTime.Now,
                        ContactReference = parentGUI,
                        PublicAccessLevelReference = "3",
                        IsPrimary = false
                    };
                    i++;
                }
            }
            else
                caseFile.Party = new AcadreServiceV7.PartyType[] { new AcadreServiceV7.PartyType() {
                CreationDate = DateTime.Now
                ,ContactReference = PrimaryContact.GUID
                ,PublicAccessLevelReference = "3"
                ,IsPrimary = true
            } };

            var userList = configurationService.GetUserList(
                new AcadreServiceV7.EmptyRequestType()).ToList();
            var user = userList.SingleOrDefault(u => u.Initials == caseResponsible);
            if (user != null)
            {
                caseFile.CaseFileManagerReference = user.Id;
            }

            createCaseRequest.CaseFile = caseFile;

            var createCaseResponse = caseService.CreateCase(createCaseRequest);
            // check for multicase (samlesag) response.
            if (createCaseResponse.CreateCaseAndAMCResult == AcadreServiceV7.CreateCaseAndAMCResultType.CaseNotCreatedAndListAMCReceived)
            {
                // create the case in all the multicases
                createCaseRequest.MultiCaseIdentifiers = createCaseResponse.MultiCaseIdentifiers;
                createCaseResponse = createCaseResponse = caseService.CreateCase(createCaseRequest);
            }

            return createCaseResponse.CaseFileIdentifier;
        }

        private AcadreServiceV7.ContactSearchResponseType GetCreateAcadreContact(string CPR)
        {
            var Contact = new AcadreServiceV7.ContactSearchResponseType();
            Contact.ContactTypeName = "Person";
            var searchContactCriterion = new AcadreServiceV7.SearchContactCriterionType2();
            searchContactCriterion.ContactTypeName = "Person";
            searchContactCriterion.SearchTerm = CPR;
            var foundContacts = contactService.SearchContacts(searchContactCriterion);
            if (foundContacts.Length > 0)
            {
                // contact already exists, read GUID and name
                Contact = foundContacts.First();
            }
            else
            {
                // forsøger at finde CPR i CPR Broker
                SimplePerson simplePerson;
                try
                {
                    simplePerson = CPRBrokerService.GetSimplePersonByCPR(CPR);
                }
                catch (Exception e)
                {
                    throw new Exception("CPR-nummeret (" + CPR + ") kunne ikke findes i CPR-registret", e);
                }
                // contact doesn't exist - create it and assign GUID
                var contact = new AcadreServiceV7.PersonType2();
                contact.PersonCivilRegistrationIdentifierStatusCode = "0";
                contact.PersonCivilRegistrationIdentifier = CPR;
                contact.PersonNameForAddressingName = Contact.ContactTitle = simplePerson.FullName;
                Contact.GUID = contactService.CreateContact(contact);
            }
            return Contact;
        }
    }
}
