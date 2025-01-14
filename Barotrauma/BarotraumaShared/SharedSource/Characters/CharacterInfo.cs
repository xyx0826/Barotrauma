using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    public enum Gender { None, Male, Female };
    public enum Race { None, White, Black, Brown, Asian };
    
    // TODO: Generating the HeadInfo could be simplified.
    partial class CharacterInfo
    {
        public class HeadInfo
        {
            private int _headSpriteId;
            public int HeadSpriteId
            {
                get { return _headSpriteId; }
                set
                {
                    _headSpriteId = value;
                    if (_headSpriteId < (int)headSpriteRange.X)
                    {
                        _headSpriteId = (int)headSpriteRange.Y;
                    }
                    if (_headSpriteId > (int)headSpriteRange.Y)
                    {
                        _headSpriteId = (int)headSpriteRange.X;
                    }
                    GetSpriteSheetIndex();
                }
            }
            public Vector2? SheetIndex { get; private set; }
            public Vector2 headSpriteRange;
            public Gender gender;
            public Race race;

            public int HairIndex { get; set; } = -1;
            public int BeardIndex { get; set; } = -1;
            public int MoustacheIndex { get; set; } = -1;
            public int FaceAttachmentIndex { get; set; } = -1;

            public XElement HairElement { get; set; }
            public XElement BeardElement { get; set; }
            public XElement MoustacheElement { get; set; }
            public XElement FaceAttachment { get; set; }
            
            public HeadInfo() { }

            public HeadInfo(int headId, Gender gender, Race race, int hairIndex = 0, int beardIndex = 0, int moustacheIndex = 0, int faceAttachmentIndex = 0)
            {
                _headSpriteId = Math.Max(headId, 1);
                this.gender = gender;
                this.race = race;
                HairIndex = hairIndex;
                BeardIndex = beardIndex;
                MoustacheIndex = moustacheIndex;
                FaceAttachmentIndex = faceAttachmentIndex;
                GetSpriteSheetIndex();
            }

            public void ResetAttachmentIndices()
            {
                HairIndex = -1;
                BeardIndex = -1;
                MoustacheIndex = -1;
                FaceAttachmentIndex = -1;
            }

            private void GetSpriteSheetIndex()
            {
                if (heads != null && heads.Any())
                {
                    var matchingHead = heads.Keys.FirstOrDefault(h => h.Gender == gender && h.Race == race && h.ID == _headSpriteId);
                    if (matchingHead != null)
                    {
                        if (heads.TryGetValue(matchingHead, out Vector2 index))
                        {
                            SheetIndex = index;
                        }
                    }
                }
            }
        }

        private HeadInfo head;
        public HeadInfo Head
        {
            get { return head; }
            set
            {
                if (head != value && value != null)
                {
                    head = value;
                    if (head.race == Race.None)
                    {
                        head.race = GetRandomRace(Rand.RandSync.Unsynced);
                    }
                    CalculateHeadSpriteRange();
                    Head.HeadSpriteId = value.HeadSpriteId;
                    HeadSprite = null;
                    AttachmentSprites = null;
                }
            }
        }

        public Dictionary<HeadPreset, Vector2> Heads
        {
            get
            {
                if (heads == null)
                {
                    LoadHeadPresets();
                }
                return heads;
            }
        }

        private static Dictionary<HeadPreset, Vector2> heads;
        public class HeadPreset : ISerializableEntity
        {
            [Serialize(Race.None, false)]
            public Race Race { get; private set; }

            [Serialize(Gender.None, false)]
            public Gender Gender { get; private set; }

            [Serialize(0, false)]
            public int ID { get; private set; }

            [Serialize("0,0", false)]
            public Vector2 SheetIndex { get; private set; }

            public string Name => $"Head Preset {Race} {Gender} {ID}";

            public Dictionary<string, SerializableProperty> SerializableProperties { get; private set; }

            public HeadPreset(XElement element)
            {
                SerializableProperties = SerializableProperty.DeserializeProperties(this, element);
            }
        }

        public XElement InventoryData;
        public XElement HealthData;
        public XElement OrderData;

        private static ushort idCounter;
        private const string disguiseName = "???";

        public bool HasNickname => Name != OriginalName;
        public string OriginalName { get; private set; }
        public string Name;
        public string DisplayName
        {
            get
            {
                if (Character == null || !Character.HideFace)
                {
                    IsDisguised = IsDisguisedAsAnother = false;
                    return Name;
                }
                else if ((GameMain.NetworkMember != null && !GameMain.NetworkMember.ServerSettings.AllowDisguises))
                {
                    IsDisguised = IsDisguisedAsAnother = false;
                    return Name;
                }

                if (Character.Inventory != null)
                {
                    var idCard = Character.Inventory.GetItemInLimbSlot(InvSlotType.Card);
                    if (idCard == null) { return disguiseName; }

                    //Disguise as the ID card name if it's equipped                    
                    string[] readTags = idCard.Tags.Split(',');
                    foreach (string tag in readTags)
                    {
                        string[] s = tag.Split(':');
                        if (s[0] == "name")
                        {
                            return s[1];
                        }
                    }
                }
                return disguiseName;
            }
        }

        private string _speciesName;
        public string SpeciesName
        {
            get
            {
                if (_speciesName == null)
                {
                    _speciesName = CharacterConfigElement.GetAttributeString("speciesname", string.Empty).ToLowerInvariant();
                }
                return _speciesName;
            }
            set { _speciesName = value; }
        }

        /// <summary>
        /// Note: Can be null.
        /// </summary>
        public Character Character;
        
        public Job Job;
        
        public int Salary;

        private Sprite headSprite;
        public Sprite HeadSprite
        {
            get
            {
                if (headSprite == null)
                {
                    LoadHeadSprite();
                }
#if CLIENT
                if (headSprite != null)
                {
                    CalculateHeadPosition(headSprite);
                }
#endif
                return headSprite;
            }
            private set
            {
                if (headSprite != null)
                {
                    headSprite.Remove();
                }
                headSprite = value;
            }
        }

        public bool OmitJobInPortraitClothing;

        private Sprite portrait;
        public Sprite Portrait
        {
            get
            {
                if (portrait == null)
                {
                    LoadHeadSprite();
                }
                return portrait;
            }
            private set
            {
                if (portrait != null)
                {
                    portrait.Remove();
                }
                portrait = value;
            }
        }

        public bool IsDisguised = false;
        public bool IsDisguisedAsAnother = false;

        public void CheckDisguiseStatus(bool handleBuff, IdCard idCard = null)
        {
            if (Character == null) { return; }

            string currentlyDisplayedName = DisplayName;

            IsDisguised = currentlyDisplayedName == disguiseName;
            IsDisguisedAsAnother = !IsDisguised && currentlyDisplayedName != Name;

            if (IsDisguisedAsAnother)
            {
                if (handleBuff)
                {
                    Character.CharacterHealth.ApplyAffliction(Character.AnimController.GetLimb(LimbType.Head), AfflictionPrefab.List.FirstOrDefault(a => a.Identifier.Equals("disguised", StringComparison.OrdinalIgnoreCase)).Instantiate(100f));
                }

                if (idCard != null)
                {
#if CLIENT
                    GetDisguisedSprites(idCard);
#endif
                    return;
                }

                if (Character.Inventory != null)
                {
                    idCard = Character.Inventory.GetItemInLimbSlot(InvSlotType.Card)?.GetComponent<IdCard>();
                    if (idCard != null)
                    {
#if CLIENT
                        GetDisguisedSprites(idCard);
#endif
                        return;
                    }
                    
                }
            }

#if CLIENT
            disguisedJobIcon = null;
            disguisedPortrait = null;
#endif

            if (handleBuff)
            {
                Character.CharacterHealth.ReduceAffliction(Character.AnimController.GetLimb(LimbType.Head), "disguised", 100f);
            }
        }

        private List<WearableSprite> attachmentSprites;
        public List<WearableSprite> AttachmentSprites
        {
            get
            {
                if (attachmentSprites == null)
                {
                    LoadAttachmentSprites(OmitJobInPortraitClothing);
                }
                return attachmentSprites;
            }
            private set
            {
                if (attachmentSprites != null)
                {
                    attachmentSprites.ForEach(s => s.Sprite?.Remove());
                }
                attachmentSprites = value;
            }
        }

        public XElement CharacterConfigElement { get; set; }

        public readonly string ragdollFileName = string.Empty;

        public bool StartItemsGiven;

        public bool IsNewHire;

        public CauseOfDeath CauseOfDeath;

        public CharacterTeamType TeamID;

        private readonly NPCPersonalityTrait personalityTrait;

        public const int MaxCurrentOrders = 3;
        public static int HighestManualOrderPriority => MaxCurrentOrders;
        public List<OrderInfo> CurrentOrders { get; } = new List<OrderInfo>();

        //unique ID given to character infos in MP
        //used by clients to identify which infos are the same to prevent duplicate characters in round summary
        public ushort ID;

        public List<string> SpriteTags
        {
            get;
            private set;
        }

        public NPCPersonalityTrait PersonalityTrait
        {
            get { return personalityTrait; }
        }

        /// <summary>
        /// Setting the value with this property also resets the head attachments. Use Head.headSpriteId if you don't want that.
        /// </summary>
        public int HeadSpriteId
        {
            get { return Head.HeadSpriteId; }
            set
            {
                Head.HeadSpriteId = value;
                HeadSprite = null;
                AttachmentSprites = null;
                ResetHeadAttachments();
            }
        }

        public readonly bool HasGenders;

        public Gender Gender
        {
            get { return Head.gender; }
            set
            {
                if (Head.gender == value) return;
                Head.gender = value;
                if (Head.gender == Gender.None)
                {
                    Head.gender = Gender.Male;
                }
                CalculateHeadSpriteRange();
                ResetHeadAttachments();
                HeadSprite = null;
                AttachmentSprites = null;
            }
        }

        public Race Race
        {
            get { return Head.race; }
            set
            {
                if (Head.race == value) { return; }
                Head.race = value;
                if (Head.race == Race.None)
                {
                    Head.race = Race.White;
                }
                CalculateHeadSpriteRange();
                ResetHeadAttachments();
                HeadSprite = null;
                AttachmentSprites = null;
            }
        }

        public int HairIndex { get => Head.HairIndex; set => Head.HairIndex = value; }
        public int BeardIndex { get => Head.BeardIndex; set => Head.BeardIndex = value; }
        public int MoustacheIndex { get => Head.MoustacheIndex; set => Head.MoustacheIndex = value; }
        public int FaceAttachmentIndex { get => Head.FaceAttachmentIndex; set => Head.FaceAttachmentIndex = value; }

        public XElement HairElement { get => Head.HairElement; set => Head.HairElement = value; }
        public XElement BeardElement { get => Head.BeardElement; set => Head.BeardElement = value; }
        public XElement MoustacheElement { get => Head.MoustacheElement; set => Head.MoustacheElement = value; }
        public XElement FaceAttachment { get => Head.FaceAttachment; set => Head.FaceAttachment = value; }

        private RagdollParams ragdoll;
        public RagdollParams Ragdoll
        {
            get
            {
                if (ragdoll == null)
                {
                    // TODO: support for variants
                    string speciesName = SpeciesName;
                    bool isHumanoid = CharacterConfigElement.GetAttributeBool("humanoid", speciesName.Equals(CharacterPrefab.HumanSpeciesName, StringComparison.OrdinalIgnoreCase));
                    ragdoll = isHumanoid 
                        ? HumanRagdollParams.GetRagdollParams(speciesName, ragdollFileName)
                        : RagdollParams.GetRagdollParams<FishRagdollParams>(speciesName, ragdollFileName) as RagdollParams;
                }
                return ragdoll;
            }
            set { ragdoll = value; }
        }

        public bool IsAttachmentsLoaded => HairIndex > -1 && BeardIndex > -1 && MoustacheIndex > -1 && FaceAttachmentIndex > -1;

        // Used for creating the data
        public CharacterInfo(string speciesName, string name = "", string originalName = "", JobPrefab jobPrefab = null, string ragdollFileName = null, int variant = 0, Rand.RandSync randSync = Rand.RandSync.Unsynced, string npcIdentifier = "")
        {
            if (speciesName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                speciesName = Path.GetFileNameWithoutExtension(speciesName).ToLowerInvariant();
            }
            ID = idCounter;
            idCounter++;
            _speciesName = speciesName;
            SpriteTags = new List<string>();
            XDocument doc = CharacterPrefab.FindBySpeciesName(_speciesName)?.XDocument;
            if (doc == null) { return; }
            CharacterConfigElement = doc.Root.IsOverride() ? doc.Root.FirstElement() : doc.Root;
            // TODO: support for variants
            head = new HeadInfo();
            HasGenders = CharacterConfigElement.GetAttributeBool("genders", false);
            if (HasGenders)
            {
                Head.gender = GetRandomGender(randSync);
            }
            Head.race = GetRandomRace(randSync);
            CalculateHeadSpriteRange();
            Head.HeadSpriteId = GetRandomHeadID(randSync);
            Job = (jobPrefab == null) ? Job.Random(Rand.RandSync.Unsynced) : new Job(jobPrefab, variant);

            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }
            else if (!string.IsNullOrEmpty(npcIdentifier) && TextManager.Get("npctitle." + npcIdentifier, true) is string npcTitle)
            {
                Name = npcTitle;
            }
            else
            { 
                name = "";
                if (CharacterConfigElement.Element("name") != null)
                {
                    string firstNamePath = CharacterConfigElement.Element("name").GetAttributeString("firstname", "");
                    if (firstNamePath != "")
                    {
                        firstNamePath = firstNamePath.Replace("[GENDER]", (Head.gender == Gender.Female) ? "female" : "male");
                        Name = ToolBox.GetRandomLine(firstNamePath, randSync);
                    }

                    string lastNamePath = CharacterConfigElement.Element("name").GetAttributeString("lastname", "");
                    if (lastNamePath != "")
                    {
                        lastNamePath = lastNamePath.Replace("[GENDER]", (Head.gender == Gender.Female) ? "female" : "male");
                        if (Name != "") Name += " ";
                        Name += ToolBox.GetRandomLine(lastNamePath, randSync);
                    }
                }
            }
            OriginalName = !string.IsNullOrEmpty(originalName) ? originalName : Name;
            personalityTrait = NPCPersonalityTrait.GetRandom(name + HeadSpriteId);         
            Salary = CalculateSalary();
            if (ragdollFileName != null)
            {
                this.ragdollFileName = ragdollFileName;
            }
            LoadHeadAttachments();
        }

        // Used for loading the data
        public CharacterInfo(XElement infoElement)
        {
            ID = idCounter;
            idCounter++;
            Name = infoElement.GetAttributeString("name", "");
            OriginalName = infoElement.GetAttributeString("originalname", null);
            string genderStr = infoElement.GetAttributeString("gender", "male").ToLowerInvariant();
            Salary = infoElement.GetAttributeInt("salary", 1000);
            Enum.TryParse(infoElement.GetAttributeString("race", "White"), true, out Race race);
            Enum.TryParse(infoElement.GetAttributeString("gender", "None"), true, out Gender gender);
            _speciesName = infoElement.GetAttributeString("speciesname", null);
            XDocument doc = null;
            if (_speciesName != null)
            {
                doc = CharacterPrefab.FindBySpeciesName(_speciesName)?.XDocument;
            }
            else
            {
                // Backwards support (human only)
                string file = infoElement.GetAttributeString("file", "");
                doc = XMLExtensions.TryLoadXml(file);
            }
            if (doc == null) { return; }
            // TODO: support for variants
            CharacterConfigElement = doc.Root.IsOverride() ? doc.Root.FirstElement() : doc.Root;
            HasGenders = CharacterConfigElement.GetAttributeBool("genders", false);
            if (HasGenders && gender == Gender.None)
            {
                gender = GetRandomGender(Rand.RandSync.Unsynced);
            }
            else if (!HasGenders)
            {
                gender = Gender.None;
            }
            RecreateHead(
                infoElement.GetAttributeInt("headspriteid", 1),
                race,
                gender,
                infoElement.GetAttributeInt("hairindex", -1),
                infoElement.GetAttributeInt("beardindex", -1),
                infoElement.GetAttributeInt("moustacheindex", -1),
                infoElement.GetAttributeInt("faceattachmentindex", -1));

            if (string.IsNullOrEmpty(Name))
            {
                if (CharacterConfigElement.Element("name") != null)
                {
                    string firstNamePath = CharacterConfigElement.Element("name").GetAttributeString("firstname", "");
                    if (firstNamePath != "")
                    {
                        firstNamePath = firstNamePath.Replace("[GENDER]", (Head.gender == Gender.Female) ? "female" : "male");
                        Name = ToolBox.GetRandomLine(firstNamePath);
                    }

                    string lastNamePath = CharacterConfigElement.Element("name").GetAttributeString("lastname", "");
                    if (lastNamePath != "")
                    {
                        lastNamePath = lastNamePath.Replace("[GENDER]", (Head.gender == Gender.Female) ? "female" : "male");
                        if (Name != "") Name += " ";
                        Name += ToolBox.GetRandomLine(lastNamePath);
                    }
                }
            }

            if (string.IsNullOrEmpty(OriginalName))
            {
                OriginalName = Name;
            }

            StartItemsGiven = infoElement.GetAttributeBool("startitemsgiven", false);
            string personalityName = infoElement.GetAttributeString("personality", "");
            ragdollFileName = infoElement.GetAttributeString("ragdoll", string.Empty);
            if (!string.IsNullOrEmpty(personalityName))
            {
                personalityTrait = NPCPersonalityTrait.List.Find(p => p.Name == personalityName);
            }      
            foreach (XElement subElement in infoElement.Elements())
            {
                if (subElement.Name.ToString().Equals("job", StringComparison.OrdinalIgnoreCase))
                {
                    Job = new Job(subElement);
                    break;
                }
            }
            LoadHeadAttachments();
        }

        public Gender GetRandomGender(Rand.RandSync randSync) => (Rand.Range(0.0f, 1.0f, randSync) < CharacterConfigElement.GetAttributeFloat("femaleratio", 0.5f)) ? Gender.Female : Gender.Male;
        public Race GetRandomRace(Rand.RandSync randSync) => new Race[] { Race.White, Race.Black, Race.Asian }.GetRandom(randSync);
        public int GetRandomHeadID(Rand.RandSync randSync) => Head.headSpriteRange != Vector2.Zero ? Rand.Range((int)Head.headSpriteRange.X, (int)Head.headSpriteRange.Y + 1, randSync) : 0;

        private List<XElement> hairs;
        private List<XElement> beards;
        private List<XElement> moustaches;
        private List<XElement> faceAttachments;

        private IEnumerable<XElement> wearables;
        public IEnumerable<XElement> Wearables
        {
            get
            {
                if (wearables == null)
                {
                    var attachments = CharacterConfigElement.Element("HeadAttachments");
                    if (attachments != null)
                    {
                        wearables = attachments.Elements("Wearable");
                    }
                }
                return wearables;
            }
        }

        public int GetIdentifier()
        {
            return GetIdentifier(Name);
        }

        public int GetIdentifierUsingOriginalName()
        {
            return GetIdentifier(OriginalName);
        }

        private int GetIdentifier(string name)
        {
            int id = ToolBox.StringToInt(name);
            id ^= HeadSpriteId;
            id ^= (int)Race << 6;
            id ^= HairIndex << 12;
            id ^= BeardIndex << 18;
            id ^= MoustacheIndex << 24;
            id ^= FaceAttachmentIndex << 30;
            if (Job != null)
            {
                id ^= ToolBox.StringToInt(Job.Prefab.Identifier);
            }
            return id;
        }

        public IEnumerable<XElement> FilterByTypeAndHeadID(IEnumerable<XElement> elements, WearableType targetType, int headSpriteId)
        {
            if (elements == null) { return elements; }
            return elements.Where(e =>
            {
                if (Enum.TryParse(e.GetAttributeString("type", ""), true, out WearableType type) && type != targetType) { return false; }
                int headId = e.GetAttributeInt("headid", -1);
                // if the head id is less than 1, the id is not valid and the condition is ignored.
                return headId < 1 || headId == headSpriteId;
            });
        }

        public IEnumerable<XElement> FilterElementsByGenderAndRace(IEnumerable<XElement> elements, Gender gender, Race race)
        {
            if (elements == null) { return elements; }
            return elements.Where(w =>
                Enum.TryParse(w.GetAttributeString("gender", "None"), true, out Gender g) && g == gender &&
                Enum.TryParse(w.GetAttributeString("race", "None"), true, out Race r) && r == race);
        }

        private void LoadHeadPresets()
        {
            if (CharacterConfigElement == null) { return; }
            heads = new Dictionary<HeadPreset, Vector2>();
            var headsElement = CharacterConfigElement.GetChildElement("heads");
            if (headsElement != null)
            {
                foreach (var head in headsElement.GetChildElements("head"))
                {
                    var preset = new HeadPreset(head);
                    heads.Add(preset, preset.SheetIndex);
                }
            }
        }

        private void CalculateHeadSpriteRange()
        {
            if (CharacterConfigElement == null) { return; }
            Head.headSpriteRange = CharacterConfigElement.GetAttributeVector2("headidrange", Vector2.Zero);
            // If the range is defined, we use it as it is
            if (Head.headSpriteRange != Vector2.Zero) { return; }
            if (heads == null)
            {
                LoadHeadPresets();
            }
            // If there are any head presets defined, use them.
            if (heads.Any())
            {
                var ids = heads.Keys.Where(h => h.Race == Race && h.Gender == Gender).Select(w => w.ID);
                ids = ids.OrderBy(id => id);
                Head.headSpriteRange = new Vector2(ids.First(), ids.Last());
            }
            // Else we calculate the range from the wearables.
            if (Head.headSpriteRange == Vector2.Zero)
            {
                var wearableElements = Wearables;
                if (wearableElements == null) { return; }
                var wearables = FilterElementsByGenderAndRace(wearableElements, head.gender, head.race).ToList();
                if (wearables == null)
                {
                    Head.headSpriteRange = Vector2.Zero;
                    return;
                }
                if (wearables.None())
                {
                    DebugConsole.ThrowError($"[CharacterInfo] No headidrange defined and no wearables matching the gender {Head.gender} and the race {Head.race} could be found. Total wearables found: {Wearables.Count()}.");
                    return;
                }
                else
                {
                    // Ignore head ids that are less than 1, because they are not supported.
                    var ids = wearables.Select(w => w.GetAttributeInt("headid", -1)).Where(id => id > 0);
                    if (ids.None())
                    {
                        DebugConsole.ThrowError($"[CharacterInfo] Wearables with matching gender and race were found but none with a valid headid! Total wearables found: {Wearables.Count()}.");
                        return;
                    }
                    ids = ids.OrderBy(id => id);
                    Head.headSpriteRange = new Vector2(ids.First(), ids.Last());
                }
            }
        }

        public void RecreateHead(int headID, Race race, Gender gender, int hairIndex, int beardIndex, int moustacheIndex, int faceAttachmentIndex)
        {
            if (HasGenders && gender == Gender.None)
            {
                gender = GetRandomGender(Rand.RandSync.Unsynced);
            }
            else if (!HasGenders)
            {
                gender = Gender.None;
            }
            if (heads == null)
            {
                LoadHeadPresets();
            }
            head = new HeadInfo(headID, gender, race, hairIndex, beardIndex, moustacheIndex, faceAttachmentIndex);
            CalculateHeadSpriteRange();
            ReloadHeadAttachments();
        }

        public void LoadHeadSprite()
        {
            foreach (XElement limbElement in Ragdoll.MainElement.Elements())
            {
                if (!limbElement.GetAttributeString("type", "").Equals("head", StringComparison.OrdinalIgnoreCase)) { continue; }

                XElement spriteElement = limbElement.Element("sprite");
                if (spriteElement == null) { continue; }

                string spritePath = spriteElement.Attribute("texture").Value;

                spritePath = spritePath.Replace("[GENDER]", (Head.gender == Gender.Female) ? "female" : "male");
                spritePath = spritePath.Replace("[RACE]", Head.race.ToString().ToLowerInvariant());
                spritePath = spritePath.Replace("[HEADID]", HeadSpriteId.ToString());

                string fileName = Path.GetFileNameWithoutExtension(spritePath);

                //go through the files in the directory to find a matching sprite
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(spritePath)))
                {
                    if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string fileWithoutTags = Path.GetFileNameWithoutExtension(file);
                    fileWithoutTags = fileWithoutTags.Split('[', ']').First();
                    if (fileWithoutTags != fileName) { continue; }

                    HeadSprite = new Sprite(spriteElement, "", file);
                    Portrait = new Sprite(spriteElement, "", file) { RelativeOrigin = Vector2.Zero };

                    //extract the tags out of the filename
                    SpriteTags = file.Split('[', ']').Skip(1).ToList();
                    if (SpriteTags.Any())
                    {
                        SpriteTags.RemoveAt(SpriteTags.Count - 1);
                    }

                    break;
                }

                break;
            }
        }

        /// <summary>
        /// Loads only the elements according to the indices, not the sprites.
        /// </summary>
        public void LoadHeadAttachments()
        {
            if (Wearables != null)
            {
                if (hairs == null)
                {
                    float commonness = Gender == Gender.Female ? 0.05f : 0.2f;
                    hairs = AddEmpty(FilterByTypeAndHeadID(FilterElementsByGenderAndRace(wearables, head.gender, head.race), WearableType.Hair, head.HeadSpriteId), WearableType.Hair, commonness);
                }
                if (beards == null)
                {
                    beards = AddEmpty(FilterByTypeAndHeadID(FilterElementsByGenderAndRace(wearables, head.gender, head.race), WearableType.Beard, head.HeadSpriteId), WearableType.Beard);
                }
                if (moustaches == null)
                {
                    moustaches = AddEmpty(FilterByTypeAndHeadID(FilterElementsByGenderAndRace(wearables, head.gender, head.race), WearableType.Moustache, head.HeadSpriteId), WearableType.Moustache);
                }
                if (faceAttachments == null)
                {
                    faceAttachments = AddEmpty(FilterByTypeAndHeadID(FilterElementsByGenderAndRace(wearables, head.gender, head.race), WearableType.FaceAttachment, head.HeadSpriteId), WearableType.FaceAttachment);
                }

                if (IsValidIndex(Head.HairIndex, hairs))
                {
                    Head.HairElement = hairs[Head.HairIndex];
                }
                else
                {
                    Head.HairElement = GetRandomElement(hairs);
                    Head.HairIndex = hairs.IndexOf(Head.HairElement);
                }
                if (IsValidIndex(Head.BeardIndex, beards))
                {
                    Head.BeardElement = beards[Head.BeardIndex];
                }
                else
                {
                    Head.BeardElement = GetRandomElement(beards);
                    Head.BeardIndex = beards.IndexOf(Head.BeardElement);
                }
                if (IsValidIndex(Head.MoustacheIndex, moustaches))
                {
                    Head.MoustacheElement = moustaches[Head.MoustacheIndex];
                }
                else
                {
                    Head.MoustacheElement = GetRandomElement(moustaches);
                    Head.MoustacheIndex = moustaches.IndexOf(Head.MoustacheElement);
                }
                if (IsValidIndex(Head.FaceAttachmentIndex, faceAttachments))
                {
                    Head.FaceAttachment = faceAttachments[Head.FaceAttachmentIndex];
                }
                else
                {
                    Head.FaceAttachment = GetRandomElement(faceAttachments);
                    Head.FaceAttachmentIndex = faceAttachments.IndexOf(Head.FaceAttachment);
                }
            }
        }

        private static List<XElement> AddEmpty(IEnumerable<XElement> elements, WearableType type, float commonness = 1)
        {
            // Let's add an empty element so that there's a chance that we don't get any actual element -> allows bald and beardless guys, for example.
            var emptyElement = new XElement("EmptyWearable", type.ToString(), new XAttribute("commonness", commonness));
            var list = new List<XElement>() { emptyElement };
            list.AddRange(elements);
            return list;
        }

        private XElement GetRandomElement(IEnumerable<XElement> elements)
        {
            var filtered = elements.Where(e => IsWearableAllowed(e));
            if (filtered.Count() == 0) { return null; }
            var element = ToolBox.SelectWeightedRandom(filtered.ToList(), GetWeights(filtered).ToList(), Rand.RandSync.Unsynced);
            return element == null || element.Name == "Empty" ? null : element;
        }

        private bool IsWearableAllowed(XElement element)
        {
            string spriteName = element.Element("sprite").GetAttributeString("name", string.Empty);
            return IsAllowed(Head.HairElement, spriteName) && IsAllowed(Head.BeardElement, spriteName) && IsAllowed(Head.MoustacheElement, spriteName) && IsAllowed(Head.FaceAttachment, spriteName);
        }

        private bool IsAllowed(XElement element, string spriteName)
        {
            if (element != null)
            {
                var disallowed = element.GetAttributeStringArray("disallow", new string[0]);
                if (disallowed.Any(s => spriteName.Contains(s)))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsValidIndex(int index, List<XElement> list) => index >= 0 && index < list.Count;

        private static IEnumerable<float> GetWeights(IEnumerable<XElement> elements) => elements.Select(h => h.GetAttributeFloat("commonness", 1f));

        partial void LoadAttachmentSprites(bool omitJob);
        
        private int CalculateSalary()
        {
            if (Name == null || Job == null) { return 0; }

            int salary = 0;
            foreach (Skill skill in Job.Skills)
            {
                salary += (int)(skill.Level * skill.Prefab.PriceMultiplier);
            }

            return (int)(salary * Job.Prefab.PriceMultiplier);
        }

        public void IncreaseSkillLevel(string skillIdentifier, float increase, Vector2 pos)
        {
            if (Job == null || (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) || Character == null) { return; }         

            if (Job.Prefab.Identifier == "assistant")
            {
                increase *= SkillSettings.Current.AssistantSkillIncreaseMultiplier;
            }

            float prevLevel = Job.GetSkillLevel(skillIdentifier);
            Job.IncreaseSkillLevel(skillIdentifier, increase);

            float newLevel = Job.GetSkillLevel(skillIdentifier);

            OnSkillChanged(skillIdentifier, prevLevel, newLevel, pos);
        }

        public void SetSkillLevel(string skillIdentifier, float level, Vector2 pos)
        {
            if (Job == null) { return; }

            var skill = Job.Skills.Find(s => s.Identifier == skillIdentifier);
            if (skill == null)
            {
                Job.Skills.Add(new Skill(skillIdentifier, level));
                OnSkillChanged(skillIdentifier, 0.0f, level, pos);
            }
            else
            {
                float prevLevel = skill.Level;
                skill.Level = level;
                OnSkillChanged(skillIdentifier, prevLevel, skill.Level, pos);
            }
        }

        partial void OnSkillChanged(string skillIdentifier, float prevLevel, float newLevel, Vector2 textPopupPos);

        public void Rename(string newName)
        {
            if (string.IsNullOrEmpty(newName)) { return; }
            // Replace the name tag of any existing id cards or duffel bags
            foreach (var item in Item.ItemList)
            {
                if (item.Prefab.Identifier != "idcard" && !item.Tags.Contains("despawncontainer")) { continue; }
                foreach (var tag in item.Tags.Split(','))
                {
                    var splitTag = tag.Split(":");
                    if (splitTag.Length < 2) { continue; }
                    if (splitTag[0] != "name") { continue; }
                    if (splitTag[1] != Name) { continue; }
                    item.ReplaceTag(tag, $"name:{newName}");
                    break;
                }
            }
            Name = newName;
        }

        public void ResetName()
        {
            Name = OriginalName;
        }

        public XElement Save(XElement parentElement)
        {
            XElement charElement = new XElement("Character");

            charElement.Add(
                new XAttribute("name", Name),
                new XAttribute("originalname", OriginalName),
                new XAttribute("speciesname", SpeciesName),
                new XAttribute("gender", Head.gender == Gender.Male ? "male" : "female"),
                new XAttribute("race", Head.race.ToString()),
                new XAttribute("salary", Salary),
                new XAttribute("headspriteid", HeadSpriteId),
                new XAttribute("hairindex", HairIndex),
                new XAttribute("beardindex", BeardIndex),
                new XAttribute("moustacheindex", MoustacheIndex),
                new XAttribute("faceattachmentindex", FaceAttachmentIndex),
                new XAttribute("startitemsgiven", StartItemsGiven),
                new XAttribute("ragdoll", ragdollFileName),
                new XAttribute("personality", personalityTrait == null ? "" : personalityTrait.Name));

            // TODO: animations?

            if (Character != null)
            {
                if (Character.AnimController.CurrentHull != null)
                {
                    charElement.Add(new XAttribute("hull", Character.AnimController.CurrentHull.ID));
                }
            }
            
            Job.Save(charElement);

            parentElement.Add(charElement);
            return charElement;
        }

        public static void SaveOrders(XElement parentElement, params OrderInfo[] orders)
        {
            if (parentElement == null || orders == null || orders.None()) { return; }
            // If an order is invalid, we discard the order and increase the priority of the following orders so
            // 1) the highest priority value will remain equal to CharacterInfo.HighestManualOrderPriority; and
            // 2) the order priorities will remain sequential.
            int priorityIncrease = 0;
            var linkedSubs = GetLinkedSubmarines();
            foreach (var orderInfo in orders)
            {
                var order = orderInfo.Order;
                if (order == null || string.IsNullOrEmpty(order.Identifier))
                {
                    DebugConsole.ThrowError("Error saving an order - the order or its identifier is null");
                    priorityIncrease++;
                    continue;
                }
                int? linkedSubIndex = null;
                bool targetAvailableInNextLevel = true;
                if (order.TargetSpatialEntity != null)
                {
                    var entitySub = order.TargetSpatialEntity.Submarine;
                    bool isOutside = entitySub == null;
                    bool canBeOnLinkedSub = !isOutside && Submarine.MainSub != null && entitySub != Submarine.MainSub && linkedSubs.Any();
                    bool isOnConnectedLinkedSub = false;
                    if (canBeOnLinkedSub)
                    {
                        for (int i = 0; i < linkedSubs.Count; i++)
                        {
                            var ls = linkedSubs[i];
                            if (!ls.LoadSub) { continue; }
                            if (ls.Sub != entitySub) { continue; }
                            linkedSubIndex = i;
                            isOnConnectedLinkedSub = Submarine.MainSub.GetConnectedSubs().Contains(entitySub);
                            break;
                        }
                    }
                    targetAvailableInNextLevel = !isOutside && GameMain.GameSession?.Campaign?.PendingSubmarineSwitch == null && (isOnConnectedLinkedSub || entitySub == Submarine.MainSub);
                    if (!targetAvailableInNextLevel)
                    {
                        if (!order.CanBeGeneralized)
                        {
                            DebugConsole.Log($"Trying to save an order ({order.Identifier}) targeting an entity that won't be connected to the main sub in the next level. The order requires a target so it won't be saved.");
                            priorityIncrease++;
                            continue;
                        }
                        else
                        {
                            DebugConsole.Log($"Saving an order ({order.Identifier}) targeting an entity that won't be connected to the main sub in the next level. The order will be saved as a generalized version.");
                        }
                    }
                }
                if (orderInfo.ManualPriority < 1)
                {
                    DebugConsole.ThrowError($"Error saving an order ({order.Identifier}) - the order priority is less than 1");
                    priorityIncrease++;
                    continue;
                }
                var orderElement = new XElement("order",
                    new XAttribute("id", order.Identifier),
                    new XAttribute("priority", orderInfo.ManualPriority + priorityIncrease),
                    new XAttribute("targettype", (int)order.TargetType));
                if (!string.IsNullOrEmpty(orderInfo.OrderOption))
                {
                    orderElement.Add(new XAttribute("option", orderInfo.OrderOption));
                }
                if (order.OrderGiver != null)
                {
                    orderElement.Add(new XAttribute("ordergiverinfoid", order.OrderGiver.Info.ID));
                }
                if (order.TargetSpatialEntity?.Submarine is Submarine targetSub)
                {
                    if (targetSub == Submarine.MainSub)
                    {
                        orderElement.Add(new XAttribute("onmainsub", true));
                    }
                    else if(linkedSubIndex.HasValue)
                    {
                        orderElement.Add(new XAttribute("linkedsubindex", linkedSubIndex));
                    }
                }
                switch (order.TargetType)
                {
                    case Order.OrderTargetType.Entity when targetAvailableInNextLevel && order.TargetEntity is Entity e:
                        orderElement.Add(new XAttribute("targetid", (uint)e.ID));
                        break;
                    case Order.OrderTargetType.Position when targetAvailableInNextLevel && order.TargetSpatialEntity is OrderTarget ot:
                        var orderTargetElement = new XElement("ordertarget");
                        var position = ot.WorldPosition;
                        if (ot.Hull != null)
                        {
                            orderTargetElement.Add(new XAttribute("hullid", (uint)ot.Hull.ID));
                            position -= ot.Hull.WorldPosition;
                        }
                        orderTargetElement.Add(new XAttribute("position", $"{position.X},{position.Y}"));
                        orderElement.Add(orderTargetElement);
                        break;
                    case Order.OrderTargetType.WallSection when targetAvailableInNextLevel && order.TargetEntity is Structure s && order.WallSectionIndex.HasValue:
                        orderElement.Add(new XAttribute("structureid", s.ID));
                        orderElement.Add(new XAttribute("wallsectionindex", order.WallSectionIndex.Value));
                        break;
                }
                parentElement.Add(orderElement);
            }
        }

        /// <summary>
        /// Save current orders to the parameter element
        /// </summary>
        public static void SaveOrderData(CharacterInfo characterInfo, XElement parentElement)
        {
            var currentOrders = new List<OrderInfo>(characterInfo.CurrentOrders);
            // Sort the current orders to make sure the one with the highest priority comes first
            currentOrders.Sort((x, y) => y.ManualPriority.CompareTo(x.ManualPriority));
            SaveOrders(parentElement, currentOrders.ToArray());
        }

        /// <summary>
        /// Save current orders to <see cref="OrderData"/>
        /// </summary>
        public void SaveOrderData()
        {
            OrderData = new XElement("orders");
            SaveOrderData(this, OrderData);
        }

        public static void ApplyOrderData(Character character, XElement orderData)
        {
            if (character == null) { return; }
            var orders = LoadOrders(orderData);
            foreach (var order in orders)
            {
                character.SetOrder(order, order.Order?.OrderGiver, speak: false, force: true);
            }
        }

        public void ApplyOrderData()
        {
            ApplyOrderData(Character, OrderData);
        }

        public static List<OrderInfo> LoadOrders(XElement ordersElement)
        {
            var orders = new List<OrderInfo>();
            if (ordersElement == null) { return orders; }
            // If an order is invalid, we discard the order and increase the priority of the following orders so
            // 1) the highest priority value will remain equal to CharacterInfo.HighestManualOrderPriority; and
            // 2) the order priorities will remain sequential.
            int priorityIncrease = 0;
            var linkedSubs = GetLinkedSubmarines();
            foreach (var orderElement in ordersElement.GetChildElements("order"))
            {
                Order order = null;
                string orderIdentifier = orderElement.GetAttributeString("id", "");
                var orderPrefab = Order.GetPrefab(orderIdentifier);
                if (orderPrefab == null)
                {
                    DebugConsole.ThrowError($"Error loading a previously saved order - can't find an order prefab with the identifier \"{orderIdentifier}\"");
                    priorityIncrease++;
                    continue;
                }
                var targetType = (Order.OrderTargetType)orderElement.GetAttributeInt("targettype", 0);
                int orderGiverInfoId = orderElement.GetAttributeInt("ordergiverinfoid", -1);
                var orderGiver = orderGiverInfoId >= 0 ? Character.CharacterList.FirstOrDefault(c => c.Info?.ID == orderGiverInfoId) : null;
                Entity targetEntity = null;
                switch (targetType)
                {
                    case Order.OrderTargetType.Entity:
                        ushort targetId = (ushort)orderElement.GetAttributeUInt("targetid", Entity.NullEntityID);
                        if (!GetTargetEntity(targetId, out targetEntity)) { continue; }
                        var targetComponent = orderPrefab.GetTargetItemComponent(targetEntity as Item);
                        order = new Order(orderPrefab, targetEntity, targetComponent, orderGiver: orderGiver);
                        break;
                    case Order.OrderTargetType.Position:
                        var orderTargetElement = orderElement.GetChildElement("ordertarget");
                        var position = orderTargetElement.GetAttributeVector2("position", Vector2.Zero);
                        ushort hullId = (ushort)orderTargetElement.GetAttributeUInt("hullid", 0);
                        if (!GetTargetEntity(hullId, out targetEntity)) { continue; }
                        if (!(targetEntity is Hull targetPositionHull))
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}) - entity with the ID {hullId} is of type {targetEntity?.GetType()} instead of Hull");
                            priorityIncrease++;
                            continue;
                        }
                        var orderTarget = new OrderTarget(targetPositionHull.WorldPosition + position, targetPositionHull);
                        order = new Order(orderPrefab, orderTarget, orderGiver: orderGiver);
                        break;
                    case Order.OrderTargetType.WallSection:
                        ushort structureId = (ushort)orderElement.GetAttributeInt("structureid", Entity.NullEntityID);
                        if (!GetTargetEntity(structureId, out targetEntity)) { continue; }
                        int wallSectionIndex = orderElement.GetAttributeInt("wallsectionindex", 0);
                        if (!(targetEntity is Structure targetStructure))
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}) - entity with the ID {structureId} is of type {targetEntity?.GetType()} instead of Structure");
                            priorityIncrease++;
                            continue;
                        }
                        order = new Order(orderPrefab, targetStructure, wallSectionIndex, orderGiver: orderGiver);
                        break;
                }
                string orderOption = orderElement.GetAttributeString("option", "");
                int manualPriority = orderElement.GetAttributeInt("priority", 0) + priorityIncrease;
                var orderInfo = new OrderInfo(order, orderOption, manualPriority);
                orders.Add(orderInfo);

                bool GetTargetEntity(ushort targetId, out Entity targetEntity)
                {
                    targetEntity = null;
                    if (targetId == Entity.NullEntityID) { return true; }
                    Submarine parentSub = null;
                    if (orderElement.GetAttributeBool("onmainsub", false))
                    {
                        parentSub = Submarine.MainSub;
                    }
                    else
                    {
                        int linkedSubIndex = orderElement.GetAttributeInt("linkedsubindex", -1);
                        if (linkedSubIndex >= 0 && linkedSubIndex < linkedSubs.Count &&
                            linkedSubs[linkedSubIndex] is LinkedSubmarine linkedSub && linkedSub.LoadSub)
                        {
                            parentSub = linkedSub.Sub;
                        }
                    }
                    if (parentSub != null)
                    {
                        targetId = GetOffsetId(parentSub, targetId);
                        targetEntity = Entity.FindEntityByID(targetId);
                    }
                    else
                    {
                        if (!orderPrefab.CanBeGeneralized)
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}). Can't find the parent sub of the target entity. The order requires a target so it can't be loaded at all.");
                            priorityIncrease++;
                            return false;
                        }
                        else
                        {
                            DebugConsole.AddWarning($"Trying to load a previously saved order ({orderIdentifier}). Can't find the parent sub of the target entity. The order doesn't require a target so a more generic version of the order will be loaded instead.");
                        }
                    }
                    return true;
                }
            }
            return orders;
        }

        private static List<LinkedSubmarine> GetLinkedSubmarines()
        {
            return Entity.GetEntities()
                .OfType<LinkedSubmarine>()
                .Where(ls => ls.Submarine == Submarine.MainSub)
                .OrderBy(e => e.ID)
                .ToList();
        }

        private static ushort GetOffsetId(Submarine parentSub, ushort id)
        {
            if (parentSub != null)
            {
                var idRemap = new IdRemap(parentSub.Info.SubmarineElement, parentSub.IdOffset);
                return idRemap.GetOffsetId(id);
            }
            return id;
        }

        public static void ApplyHealthData(Character character, XElement healthData)
        {
            if (healthData != null) { character?.CharacterHealth.Load(healthData); }
        }

        public void ReloadHeadAttachments()
        {
            ResetLoadedAttachments();
            LoadHeadAttachments();
        }

        public void ResetHeadAttachments()
        {
            ResetAttachmentIndices();
            ResetLoadedAttachments();
        }

        private void ResetAttachmentIndices()
        {
            Head.ResetAttachmentIndices();
        }

        private void ResetLoadedAttachments()
        {
            hairs = null;
            beards = null;
            moustaches = null;
            faceAttachments = null;
        }

        public void ClearCurrentOrders()
        {
            CurrentOrders.Clear();
        }

        public void Remove()
        {
            Character = null;
            HeadSprite = null;
            Portrait = null;
            AttachmentSprites = null;
        }
    }
}
