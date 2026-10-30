using DOL.Database;
using DOL.Database.Transaction;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.UnitTests.Gameserver
{
    public class FakePacketLib : PacketLib1124
    {
        public FakePacketLib() : base(null) { }

#pragma warning disable CS0672
        public override void SendCheckLOS(GameObject Checker, GameObject Target, CheckLOSResponse callback) { }
#pragma warning restore CS0672
        public override void SendUpdatePlayer() { }
        public override void SendUpdateMaxSpeed() { }
        public override void SendAttackMode(bool attackState) { }
        public override void SendPlayerPositionAndObjectID() { }
        public override void SendGroupWindowUpdate() { }
        public override void SendGroupMembersUpdate(bool updateIcons, bool updatePlayerList) { }
        public override void SendGroupMemberUpdate(bool updateIcons, bool updateMap, GameLiving living) { }
        public string LastTimerTitle { get; set; }
        public int LastTimerSeconds { get; set; }
        public bool TimerClosed { get; set; }
        public ushort LastSpellEffect { get; set; }
        public ushort LastSoundEffect { get; set; }
        public List<string> SentMessages { get; } = new List<string>();
        public List<(string Message, eChatType Type, eChatLoc Loc)> SentMessageDetails { get; } = new List<(string, eChatType, eChatLoc)>();

        public override void SendMessage(string msg, eChatType type, eChatLoc loc)
        {
            SentMessages.Add(msg);
            SentMessageDetails.Add((msg, type, loc));
        }

        public override void SendTimerWindow(string title, int seconds)
        {
            LastTimerTitle = title;
            LastTimerSeconds = seconds;
            TimerClosed = false;
        }

        public override void SendCloseTimerWindow()
        {
            TimerClosed = true;
        }

        public override void SendSpellEffectAnimation(GameObject spellCaster, GameObject spellTarget, ushort spellid, ushort boltTime, bool noSound, byte success)
        {
            LastSpellEffect = spellid;
        }

        public override void SendSoundEffect(ushort soundId, Position pos, ushort radius)
        {
            LastSoundEffect = soundId;
        }
    }

    public class FakeRegion : Region
    {
        public long fakeElapsedTime { get { return fakeTimeManager.fakeTime; } set { fakeTimeManager.fakeTime = value; } }
        public FakeTimeManager fakeTimeManager = new FakeTimeManager();

        public FakeRegion() : base(null, new RegionData()) { }

        public override long Time => TimeManager.CurrentTime;

        public override ushort ID => 0;

        public override GameTimer.TimeManager TimeManager => fakeTimeManager;
    }

    public class FakeTimeManager : GameTimer.TimeManager
    {
        public long fakeTime = -1;

        public FakeTimeManager() : base("FakeTimer") { }

        public override long CurrentTime => fakeTime;
        public override bool Start() => true;
        protected override void InsertTimer(GameTimer t, int offsetTick) { }
        protected override void RemoveTimer(GameTimer t) { }

        protected override void Init() { }
    }

    public class FakeServer : GameServer
    {
        private IObjectDatabase database = new FakeDatabase();

        protected override IObjectDatabase DataBaseImpl => database;
        protected override void CheckAndInitDB() { }
        public override byte[] AcquirePacketBuffer() => Array.Empty<byte>();
        public void SetDatabase(IObjectDatabase database) { this.database = database; }

        public static void Load() => LoadTestDouble(new FakeServer());
    }

    public class FakeDatabase : IObjectDatabase
    {
        public IList<DataObject> SelectObjectReturns { get; set; } = new List<DataObject>();

        public bool AddObject(DataObject dataObject) => throw new NotImplementedException();
        public bool AddObject(IEnumerable<DataObject> dataObjects) => throw new NotImplementedException();
        public bool DeleteObject(DataObject dataObject) => throw new NotImplementedException();
        public bool DeleteObject(IEnumerable<DataObject> dataObjects) => throw new NotImplementedException();
        public string Escape(string rawInput) => throw new NotImplementedException();
        public bool ExecuteNonQuery(string rawQuery) => throw new NotImplementedException();
        public void FillObjectRelations(DataObject dataObject) => throw new NotImplementedException();
        public void FillObjectRelations(IEnumerable<DataObject> dataObject) => throw new NotImplementedException();
        public TObject FindObjectByKey<TObject>(object key) where TObject : DataObject => throw new NotImplementedException();
        public IList<TObject> FindObjectsByKey<TObject>(IEnumerable<object> keys) where TObject : DataObject => throw new NotImplementedException();
        public int GetObjectCount<TObject>() where TObject : DataObject => throw new NotImplementedException();
        public int GetObjectCount<TObject>(string whereExpression) where TObject : DataObject => throw new NotImplementedException();
        public void RegisterDataObject(Type dataObjectType) => throw new NotImplementedException();
        public bool SaveObject(DataObject dataObject) => true;
        public bool SaveObject(IEnumerable<DataObject> dataObjects) => throw new NotImplementedException();
        public IList<TObject> SelectAllObjects<TObject>() where TObject : DataObject => throw new NotImplementedException();

        public TObject SelectObject<TObject>(WhereClause whereClause) where TObject : DataObject => (TObject)SelectObjectReturns.FirstOrDefault();
        public IList<TObject> SelectObjects<TObject>(WhereClause whereClause) where TObject : DataObject => new List<TObject>();
        public IList<IList<TObject>> MultipleSelectObjects<TObject>(IEnumerable<WhereClause> whereClauseBatch) where TObject : DataObject => throw new NotImplementedException();

        public bool UpdateInCache<TObject>(object key) where TObject : DataObject => false;
        public bool UpdateObjsInCache<TObject>(IEnumerable<object> keys) where TObject : DataObject => throw new NotImplementedException();
    }

    public class UtilChanceIsHundredPercent : Util
    {
        protected override int RandomImpl(int min, int max) => 100;

        public static void Enable()
        {
            Util.LoadTestDouble(new UtilChanceIsHundredPercent());
        }
    }
}
