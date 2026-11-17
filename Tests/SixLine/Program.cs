using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Luban;
using cfg;

class Program
{
    static readonly string ProjectRoot = FindRoot();
    static string FindRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataTables", "gen.sh"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("请在项目目录运行测试");
    }
    static DSixLineRule Rules(params (int index, int value)[] changes)
    {
        var v = new[] { 1,12,10,2,5,6,10,250,80,1000,3,5,2 };
        foreach(var c in changes) v[c.index]=c.value;
        var buf=new ByteBuf(); foreach(var x in v) buf.WriteInt(x);
        return new DSixLineRule(new ByteBuf(buf.CopyData()));
    }
    static void Assert(bool ok, string msg) { if(!ok) throw new Exception(msg); }
    static object Call(SixLineLogic l,string name,params object[] args) => typeof(SixLineLogic).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(l,args);
    static SixLineLogic New(long seed=17) => new SixLineLogic(seed,Rules());
    static void Test(string n, Action body) { body(); Console.WriteLine("PASS "+n); }
    static void Main()
    {
        Test("Luban binary + configured geometry",()=>{
            var tables=new Tables(n=>new ByteBuf(File.ReadAllBytes(Path.Combine(ProjectRoot,"Assets/Res/LubanData/Bin",n+".bytes"))));
            var l=new SixLineLogic(1,tables.TbSixLineRule.Get(1));Assert(l.TotalCells==114,"default board");
            foreach(var size in new[]{(6,6),(13,11),(32,20)}) {
                l=new SixLineLogic(1,Rules((1,size.Item1),(2,size.Item2)));
                Assert(l.TotalCells == (size.Item1/2)*(size.Item2*2-1)+(size.Item1%2)*size.Item2,"board allocation");
                for(int r=0;r<l.Rows;r++) for(int c=0;c<l.RowCells(r);c++) for(int d=0;d<6;d++) {
                    var n=l.Neighbor(r,c,d); Assert(n>=-1&&n<l.TotalCells,"neighbor bounds");
                }
            }
        });
        Test("reference rules and honeycomb contact metric", () =>
        {
            var tables = new Tables(n => new ByteBuf(File.ReadAllBytes(Path.Combine(ProjectRoot, "Assets/Res/LubanData/Bin", n + ".bytes"))));
            var logic = new SixLineLogic(1, tables.TbSixLineRule.Get(1));
            Assert(logic.Rows == 12 && logic.Columns == 10 && logic.ClearCount == 6 && logic.ColorMax == 5, "reference rules changed");
            var half = Fix.FromDouble(.5);
            var distanceSquared = half * half + SixLineLogic.RowPitch * SixLineLogic.RowPitch;
            Assert(Math.Abs(distanceSquared.Raw - Fix.One.Raw) <= 2, "triangle is not equilateral");
            var p = logic.Players[0];
            p.Board.Set(0, 2, 1);
            var clamped = (Fix)Call(logic, "ClampXByBalls", p, 0, Fix.Zero, Fix.One, Fix.One, 1);
            Assert(clamped > Fix.FromDouble(.5) && clamped < Fix.FromDouble(.6), "collision ignored honeycomb row pitch");
        });
        Test("single/double/chain attacks and deferred landing",()=>{
            var l=New();var a=l.Players[0];var b=l.Players[1];
            a.RoundCount=2;a.RoundClusters[0]=1;a.RoundClusters[1]=2;
            Call(l,"ResolveGarbage");Assert(a.SentThisFrame==10&&b.PendingGarbage==10&&b.TotalReceived==0,"3+(5+2)");
            a.RoundCount=0;b.LockedThisFrame=true;Call(l,"ResolveGarbage");
            Assert(b.PendingGarbage==0&&b.TotalReceived==10,"pending delivery");
        });
        Test("special cross sends double attack",()=>{
            var l=New();var p=l.Players[0];p.Board.Set(0,0,1);p.Board.Set(0,1,1);p.Board.Set(0,2,1);p.Board.Set(0,3,1);p.Board.Set(0,4,1);p.Board.Set(0,5,1);
            for(int r=1;r<6;r++) p.Board.Set(r,r/2,1);
            Call(l,"SettleChains",p);Assert(p.RoundPatterns[0]>=2,"cross patterns");
            Call(l,"ResolveGarbage");Assert(p.SentThisFrame>=5,"cross bonus");
        });
        Test("ordinary cluster, actual cascade and six special shapes",()=>{
            var l=New();var p=l.Players[0];
            foreach(var cell in new[]{(0,0),(0,1),(0,2),(1,0),(1,1),(2,0)})p.Board.Set(cell.Item1,cell.Item2,1);
            Call(l,"SettleChains",p);Assert(p.RoundCount==1&&p.RoundPatterns[0]==0&&p.RoundCells[0]==6,"ordinary cluster");
            l=New();p=l.Players[0];for(int c=0;c<6;c++)p.Board.Set(0,c,1);
            for(int c=0;c<3;c++){p.Board.Set(2,c,2);p.Board.Set(4,c,2);}
            Call(l,"SettleChains",p);Assert(p.RoundCount==2&&p.RoundCells[0]==6&&p.RoundCells[1]==6,"cascade didn't rescan");
            Call(l,"ResolveGarbage");Assert(p.SentThisFrame>=8,"cascade attack");
            for(int phase=0;phase<2;phase++)foreach(bool reverse in new[]{false,true}){
                l=New();p=l.Players[0];int row=reverse?phase+2:phase;int d=reverse?-1:1;
                int c=1;bool wide=SixLineLogic.RowIsWide(row);int mid=wide?c:c+1;int tip=wide?c+1:c+2;
                for(int x=c;x<c+3;x++)p.Board.Set(row,x,1);
                p.Board.Set(row+d,mid,1);p.Board.Set(row+d,mid+1,1);p.Board.Set(row+2*d,tip,1);
                object[] args={p,0};var mask=(int)Call(l,"ScanSpecial",args);Assert(mask==1&&(int)args[1]>=1,"pyramid phase");
            }
            for(int phase=0;phase<2;phase++)foreach(int center in new[]{0,3}){
                l=New();p=l.Players[0];int top=phase+2;int c=2;bool wide=SixLineLogic.RowIsWide(top);
                p.Board.Set(top,c,1);p.Board.Set(top,c+1,1);p.Board.Set(top-2,c,1);p.Board.Set(top-2,c+1,1);
                p.Board.Set(top-1,wide?c-1:c,1);p.Board.Set(top-1,wide?c+1:c+2,1);p.Board.Set(top-1,wide?c:c+1,center);
                object[] args={p,0};Assert((int)Call(l,"ScanSpecial",args)==1,"ring depends on center or phase");
            }
        });
        Test("no repeated attack next tick",()=>{
            var l=New();var p=l.Players[0];p.RoundCount=1;p.RoundClusters[0]=1;
            Call(l,"ResolveGarbage");var pending=l.Players[1].PendingGarbage;
            l.Tick(1,null);Assert(l.Players[1].PendingGarbage==pending&&p.RoundCount==0&&p.SentThisFrame==0,"stale result repeated");
        });
        Test("settling leaves no unsupported balls above the floor",()=>{
            foreach(var config in new[]{Rules(),Rules((1,13),(2,9))}){
                var l=new SixLineLogic(1,config);var p=l.Players[0];p.Board.Set(1,2,1);Call(l,"SettlePass",p);
                Assert(p.Board.Get(1,2)==0&&p.Board.Get(0,2)==1,"narrow bottom row floated");
                var random=new XRng(123);for(int r=2;r<l.Rows;r++)for(int c=0;c<l.RowCells(r);c++)
                    if(random.NextInt(3)==0)p.Board.Set(r,c,random.NextInt(1,6));
                Call(l,"SettlePass",p);
                for(int r=1;r<l.Rows;r++)for(int c=0;c<l.RowCells(r);c++)if(p.Board.Get(r,c)!=0){
                    int a=l.Neighbor(r,c,5),b=l.Neighbor(r,c,4);
                    Assert((a<0||p.Board.Cells[a]!=0)&&(b<0||p.Board.Cells[b]!=0),"unsupported ball remains");
                }
            }
        });
        Test("danger line, draw, terminal freeze",()=>{
            var l=New();l.Players[0].Board.Set(l.Rows-l.DangerRows,0,1);l.Tick(1,null);
            Assert(l.GameOver&&l.WinnerIndex==1,"winner");var hash=l.StateHash();l.Tick(2,null);Assert(hash==l.StateHash(),"terminal mutated");
            l=New();for(int i=0;i<2;i++)l.Players[i].Board.Set(l.Rows-l.DangerRows,0,1);l.Tick(1,null);
            Assert(l.GameOver&&l.WinnerIndex==-1,"draw");
        });
        Test("out of board lock cannot discard balls silently",()=>{
            var l=New();var p=l.Players[0];p.Tri.Y=Fix.FromInt(l.Rows);Call(l,"Collapse",p);Call(l,"CheckGameOver");
            Assert(p.Overflow&&l.GameOver&&l.WinnerIndex==1,"overflow not lost");
        });
        Test("same frame mutual garbage and garbage defeat",()=>{
            var l=New();for(int i=0;i<2;i++){var p=l.Players[i];p.LockedThisFrame=true;p.RoundCount=1;p.RoundClusters[0]=1;}
            Call(l,"ResolveGarbage");Assert(l.Players[0].TotalReceived==3&&l.Players[1].TotalReceived==3,"sequential bias");
            l=New();var b=l.Players[1];b.LockedThisFrame=true;b.PendingGarbage=l.TotalCells+1;
            Call(l,"ResolveGarbage");Call(l,"CheckGameOver");Assert(l.GameOver&&l.WinnerIndex==0,"garbage should lose");
        });
        Test("same nth piece despite different drop timing + long stream allocation",()=>{
            var l=New();var a=l.Players[0];var b=l.Players[1];
            var colors=new int[200,3];for(int n=0;n<200;n++){for(int c=0;c<3;c++)colors[n,c]=a.Tri.Colors[c];Call(l,"DrawNext",a);}
            for(int n=0;n<200;n++){for(int c=0;c<3;c++)Assert(colors[n,c]==b.Tri.Colors[c],"sequence divergence");Call(l,"DrawNext",b);}
            for(int n=0;n<50;n++)Call(l,"DrawNext",a);
            var draw=(Action<SixLineLogic.Player>)typeof(SixLineLogic).GetMethod("DrawNext",BindingFlags.Instance|BindingFlags.NonPublic).CreateDelegate(typeof(Action<SixLineLogic.Player>),l);
            draw(a);long start=GC.GetAllocatedBytesForCurrentThread();for(int n=0;n<100000;n++)draw(a);
            Assert(GC.GetAllocatedBytesForCurrentThread()-start==0,"piece stream allocates");
        });
        Test("deterministic full games for multiple seeds and configurations",()=>{
            var inputs=new List<SlInput>(8);var rng=new XRng(7301);int ended=0;
            foreach(var config in new[]{Rules(),Rules((1,14),(2,12),(5,5)),Rules((1,13),(2,9))})
            for(int seed=1;seed<=12;seed++){
                var a=new SixLineLogic(seed,config);var b=new SixLineLogic(seed,config);
                for(int f=1;f<=20000&&!a.GameOver;f++){
                    inputs.Clear();for(int i=0;i<2;i++){
                        inputs.Add(new SlInput{PlayerIndex=i,OpType=SlOp.FastDrop});
                        var op=rng.NextInt(8);if(op<4)inputs.Add(new SlInput{PlayerIndex=i,OpType=op+1});
                    }
                    a.Tick(f,inputs);b.Tick(f,inputs);Assert(a.StateHash()==b.StateHash(),"replay mismatch");
                    for (int i = 0; i < 2; i++) if (a.Players[i].LockedThisFrame)
                    {
                        Assert(a.Players[i].VisualEventCount == b.Players[i].VisualEventCount, "motion count mismatch");
                        for (int e = 0; e < a.Players[i].VisualEventCount; e++)
                            Assert(a.Players[i].VisualEvents[e].Equals(b.Players[i].VisualEvents[e]), "motion replay mismatch");
                    }
                }
                Assert(a.GameOver,"never ends");ended++;
            }
            Console.WriteLine("  completed games: "+ended);
        });
        Test("continuous contact never pulls the falling triangle upward", () =>
        {
            var logic = New(); var p = logic.Players[0];
            p.Board.Set(0, 4, 1); p.Tri.X = Fix.FromDouble(3.2);
            var previous = p.Tri.Y;
            var ops = new List<SlInput> { new SlInput { PlayerIndex = 0, OpType = SlOp.FastDrop } };
            for (int f = 1; f < 30 && p.Tri.State == SixLineLogic.PieceState.Falling; f++)
            {
                logic.Tick(f, ops);
                Assert(p.Tri.Y <= previous, "contact snapped upward"); previous = p.Tri.Y;
            }
            Assert(p.Tri.State == SixLineLogic.PieceState.Resting && p.Tri.Y.Raw % Fix.One.Raw != 0, "contact snapped to integer row");
            logic = New(); p = logic.Players[0];
            var center = p.Tri.Y + Fix.FromInt(5) / Fix.FromInt(6);
            Assert((bool)Call(logic, "TryRotate", p, 1), "rotation rejected in empty board");
            var nextCenter = p.Tri.Y + Fix.FromInt(7) / Fix.FromInt(6);
            Assert(Math.Abs(center.Raw - nextCenter.Raw) <= 2, "rotation moved triangle center");
        });
        Test("roll records a circular support path and crest contact", () =>
        {
            var logic = New(); var p = logic.Players[0];
            p.Board.Set(0, 2, 1); p.Board.Set(2, 2, 2);
            Call(logic, "BeginPresentation", p); Call(logic, "SettlePass", p);
            bool crest = false, roll = false;
            for (int i = 0; i < p.VisualEventCount; i++)
            {
                var e = p.VisualEvents[i];
                if (e.Color != 2) continue;
                if (e.Kind == SlVisualKind.Fall && e.ToY == Fix.FromDouble(1.5)) crest = true;
                if (e.Kind != SlVisualKind.Roll) continue;
                roll = true;
                var dx = e.FromX - e.PivotX; var dy = e.FromY - e.PivotY;
                Assert(Math.Abs((dx * dx + dy * dy).Raw - Fix.One.Raw) <= 4, "roll starts away from supporting circle");
                dx = e.ToX - e.PivotX; dy = e.ToY - e.PivotY;
                Assert(Math.Abs((dx * dx + dy * dy).Raw - Fix.One.Raw) <= 4, "roll ends inside supporting circle");
            }
            Assert(crest && roll, "missing crest/roll stages");
        });
        Test("rolling arc cannot pass through a neighboring ball", () =>
        {
            var logic = New(); var p = logic.Players[0];
            p.Board.Set(0, 2, 1); p.Board.Set(1, 3, 2);
            var args = new object[] { p.Board.Cells, logic.RowStart(0) + 2,
                Fix.FromInt(3), Fix.FromDouble(.5) + SixLineLogic.RowPitch,
                Fix.FromDouble(3.5), Fix.FromDouble(.5) };
            Assert(!(bool)Call(logic, "ArcFree", args), "roll crossed a side neighbor");
            p.Board.Set(1, 3, 0);
            Assert((bool)Call(logic, "ArcFree", args), "open rolling path rejected");
        });
        Test("chain presentation preserves piece identity and phase order", () =>
        {
            var logic = New(); var p = logic.Players[0];
            for (int c = 0; c < 6; c++) p.Board.Set(0, c, 1);
            for (int c = 0; c < 3; c++) { p.Board.Set(2, c, 2); p.Board.Set(4, c, 2); }
            Call(logic, "BeginPresentation", p); Call(logic, "SettleChains", p);
            int firstClearEnd = 0, secondClearStart = int.MaxValue; bool falling = false;
            var paths = new Dictionary<int, SlVisualEvent>();
            for (int i = 0; i < p.VisualEventCount; i++)
            {
                var e = p.VisualEvents[i];
                Assert(e.PieceId > 0 && e.DurationFrames > 0 && e.StartFrame + e.DurationFrames <= p.VisualEndFrame, "invalid event");
                if (paths.TryGetValue(e.PieceId, out var old))
                {
                    Assert(old.Kind != SlVisualKind.Clear && old.Color == e.Color, "identity recycled during chain");
                    Assert(old.StartFrame + old.DurationFrames <= e.StartFrame, "same piece has overlapping motions");
                    Assert(Math.Abs(old.ToX.Raw - e.FromX.Raw) <= 4 && Math.Abs(old.ToY.Raw - e.FromY.Raw) <= 4, "trajectory has discontinuity");
                }
                paths[e.PieceId] = e;
                if (e.Kind == SlVisualKind.Clear && e.Chain == 1) firstClearEnd = Math.Max(firstClearEnd, e.StartFrame + e.DurationFrames);
                if (e.Kind == SlVisualKind.Clear && e.Chain == 2) secondClearStart = Math.Min(secondClearStart, e.StartFrame);
                if (e.Kind == SlVisualKind.Fall)
                {
                    falling = true; Assert(e.StartFrame >= firstClearEnd, "fell before first clear completed");
                }
            }
            Assert(falling && secondClearStart > firstClearEnd, "chain collapsed into one visual step");
        });
        Test("settlement blocks new input until fixed logical frame window ends", () =>
        {
            var logic = New(); var p = logic.Players[0];
            p.Tri.X = Fix.Zero; p.Tri.Y = Fix.Zero; p.Tri.State = SixLineLogic.PieceState.Resting; p.RestEndFrame = 0;
            var sequence = p.SeqPos; logic.Tick(1, null);
            Assert(p.Tri.State == SixLineLogic.PieceState.None && p.SeqPos == sequence, "spawned during settlement");
            var ready = p.ResolveUntilFrame;
            Assert(ready > 1 + SixLineLogic.SnapFrames, "missing animation window");
            logic.Tick(ready - 1, new List<SlInput> { new SlInput { PlayerIndex = 0, OpType = SlOp.RotateCW } });
            Assert(p.Tri.State == SixLineLogic.PieceState.None && p.SeqPos == sequence, "input consumed by hidden next piece");
            logic.Tick(ready, null);
            Assert(p.Tri.State == SixLineLogic.PieceState.Falling && p.SeqPos == sequence + 3, "new piece did not spawn after window");
        });
        Test("driver catch-up holds input and consumes rotation once",()=>{
            var logic=New();var touch=new SlTouchInput(0);var driver=new SlLocalDriver(logic);
            driver.RegisterInput(touch);UnityEngine.Time.realtimeSinceStartupAsDouble=0;
            touch.Press(SlOp.MoveLeft);touch.Press(SlOp.RotateCW);driver.MyUpdate(0,0);
            UnityEngine.Time.realtimeSinceStartupAsDouble=.16;driver.MyUpdate(.16f,.16f);
            Assert(driver.CurFrameId==3&&logic.Players[0].Tri.Rotation==1,"edge repeated during catch-up");
            Assert(logic.Players[0].Tri.X==Fix.FromDouble(3.25),"held input lost on catch-up");
            touch.Release(SlOp.MoveLeft);UnityEngine.Time.realtimeSinceStartupAsDouble=.32;driver.MyUpdate(.16f,.16f);
            Assert(logic.Players[0].Tri.X==Fix.FromDouble(3.25),"released input still held");
            driver.Pause();touch.Press(SlOp.MoveRight);driver.MyUpdate(1,1);driver.Resume();
            UnityEngine.Time.realtimeSinceStartupAsDouble=100;driver.MyUpdate(1,1);
            Assert(driver.CurFrameId==6,"resumed into catch-up");
            UnityEngine.Application.isFocused=false;driver.MyUpdate(1,1);
            UnityEngine.Application.isFocused=true;UnityEngine.Time.realtimeSinceStartupAsDouble=200;driver.MyUpdate(1,1);
            UnityEngine.Time.realtimeSinceStartupAsDouble=200.06;driver.MyUpdate(.06f,.06f);
            Assert(driver.CurFrameId==7&&logic.Players[0].Tri.X==Fix.FromDouble(3.25),"focus loss retained touch input");
            driver.Dispose();Assert(!CoreMgr.Update.Registered.Contains(driver),"driver remained registered");
        });
        Test("driver terminal frame stops catch-up while render continues",()=>{
            var logic=New();logic.Players[0].Board.Set(logic.Rows-1,0,1);
            var driver=new SlLocalDriver(logic);float dt=0;driver.OnRenderFrame+=(a,d)=>dt=d;
            UnityEngine.Time.realtimeSinceStartupAsDouble=0;driver.MyUpdate(0,0);
            UnityEngine.Time.realtimeSinceStartupAsDouble=1;driver.MyUpdate(1,1);
            Assert(logic.GameOver&&driver.CurFrameId==1,"terminal overrun");
            driver.MyUpdate(.05f,.05f);Assert(dt==.05f,"terminal animations frozen");driver.Dispose();
        });
        Test("keyboard players have independent bindings",()=>{
            var p0=new SlKeyboardInput(0);var p1=new SlKeyboardInput(1);var list=new List<SlInput>();
            UnityEngine.Input.Held.Add(UnityEngine.KeyCode.A);UnityEngine.Input.Held.Add(UnityEngine.KeyCode.DownArrow);
            UnityEngine.Input.Down.Add(UnityEngine.KeyCode.E);p0.Collect(list);p0.CollectHeld(list);p1.Collect(list);p1.CollectHeld(list);
            Assert(list.Count==3&&list[0].PlayerIndex==0&&list[0].OpType==SlOp.RotateCW
                &&list[1].PlayerIndex==0&&list[1].OpType==SlOp.MoveLeft
                &&list[2].PlayerIndex==1&&list[2].OpType==SlOp.FastDrop,"key bindings interfere");
            UnityEngine.Input.Held.Clear();UnityEngine.Input.Down.Clear();
        });
        Test("steady Tick and lock+garbage Tick allocate zero",()=>{
            var l=New();var inputs=new List<SlInput>(8);
            for(int n=0;n<100;n++) {Reset(l);l.Tick(n,null);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int n=0;n<10000;n++){Reset(l);l.Tick(n,null);}
            Assert(GC.GetAllocatedBytesForCurrentThread()==before,"steady tick GC");
            for(int n=0;n<100;n++) {Reset(l);SetupLock(l);l.Tick(n,null);}
            before=GC.GetAllocatedBytesForCurrentThread();
            for(int n=0;n<10000;n++){Reset(l);SetupLock(l);l.Tick(n,null);}
            Assert(GC.GetAllocatedBytesForCurrentThread()==before,"lock tick GC");
        });
    }
    static void Reset(SixLineLogic l){foreach(var p in l.Players){Array.Clear(p.Board.Cells);p.Tri.State=SixLineLogic.PieceState.Falling;p.Tri.Y=Fix.FromInt(l.Rows);p.Overflow=false;p.PendingGarbage=0;}}
    static void SetupLock(SixLineLogic l){foreach(var p in l.Players){p.Tri.State=SixLineLogic.PieceState.Resting;p.Tri.X=Fix.Zero;p.Tri.Y=Fix.Zero;p.RestEndFrame=0;p.PendingGarbage=3;for(int c=0;c<3;c++)p.Tri.Colors[c]=1;for(int c=3;c<6;c++)p.Board.Set(0,c,1);}}
}
