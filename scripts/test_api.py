#!/usr/bin/env python3
"""Real HTTP + database integration tests. Always boots an isolated local fixture."""
import concurrent.futures as futures
import hashlib, hmac, json, os, pathlib, secrets, shutil, socket, sqlite3, subprocess, sys, tempfile, time, uuid
from datetime import datetime, timedelta, timezone
import http.cookiejar
import urllib.request
import urllib.error
import json as json_module

class HttpResponse:
    def __init__(self, response):
        self.status_code = response.code
        self.text = response.read().decode('utf-8')
        response.close()
    def json(self): return json_module.loads(self.text)

class HttpSession:
    def __init__(self): self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def request(self, method, url, json=None, data=None, headers=None, timeout=20):
        h=dict(headers or {})
        if json is not None:
            data=json_module.dumps(json).encode();h['Content-Type']='application/json'
        elif isinstance(data,str): data=data.encode()
        req=urllib.request.Request(url,data=data,headers=h,method=method)
        try: response=self.opener.open(req,timeout=timeout)
        except urllib.error.HTTPError as error: response=error
        return HttpResponse(response)

class Http:
    Session=HttpSession
    RequestException=(urllib.error.URLError,TimeoutError)
    @staticmethod
    def get(url,**kwargs): return HttpSession().request('GET',url,**kwargs)
    @staticmethod
    def post(url,**kwargs): return HttpSession().request('POST',url,**kwargs)
requests=Http
ROOT = pathlib.Path(__file__).resolve().parents[1]
DOTNET = os.environ.get('EVENTHUB_DOTNET') or shutil.which('dotnet') or '/workspace/.dotnet/dotnet'
checks = 0
sql_context = None

class SqlFixture:
    def __init__(self):
        self.name='eventhub-sql-test-'+uuid.uuid4().hex[:8]
        self.password=secrets.token_urlsafe(25)+'!Aa2'
        with socket.socket() as sock: sock.bind(('127.0.0.1',0));self.port=sock.getsockname()[1]
        self.env={**os.environ,'MSSQL_SA_PASSWORD':self.password,'SQLCMDPASSWORD':self.password}
    def __enter__(self):
        subprocess.run(['docker','run','-d','--name',self.name,'-e','ACCEPT_EULA=Y','-e','MSSQL_SA_PASSWORD','-p',f'127.0.0.1:{self.port}:1433','mcr.microsoft.com/mssql/server:2022-latest'],env=self.env,check=True,stdout=subprocess.DEVNULL)
        print('Waiting for the isolated SQL Server fixture…',flush=True)
        for _ in range(150):
            try:self.command('SELECT 1',database='master');return self
            except RuntimeError:time.sleep(.5)
        self.__exit__(None,None,None);raise RuntimeError('SQL Server fixture timed out')
    def command(self,query,database='EventHubIntegration'):
        result=subprocess.run(['docker','exec','-e','SQLCMDPASSWORD',self.name,'/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-C','-b','-h','-1','-W','-s','|','-d',database,'-Q','SET NOCOUNT ON; '+query],env=self.env,text=True,capture_output=True)
        if result.returncode:raise RuntimeError(result.stdout+result.stderr)
        return [line.strip() for line in result.stdout.splitlines() if line.strip()]
    def __exit__(self,*args):subprocess.run(['docker','rm','-f',self.name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    @property
    def connection(self):return f'Server=127.0.0.1,{self.port};Database=EventHubIntegration;User Id=sa;Password={self.password};TrustServerCertificate=True'

class SqlRows:
    def __init__(self,rows):self.rows=rows
    def fetchone(self):
        if not self.rows:return None
        return tuple(int(v) if v.lstrip('-').isdigit() else v for v in self.rows[0].split('|'))

class SqlDb:
    def __enter__(self):return self
    def __exit__(self,*args):pass
    def execute(self,query,parameters=()):
        query=query.replace('"',"'")
        for value in parameters:
            literal=str(value) if isinstance(value,int) else "N'"+str(value).replace("'","''")+"'"
            query=query.replace('?',literal,1)
        return SqlRows(sql_context.command(query))

def open_db(path):return SqlDb() if sql_context else sqlite3.connect(path)

def check(condition, label):
    global checks
    assert condition, label
    checks += 1
    print(f'PASS {checks:02d} {label}', flush=True)

class Client:
    def __init__(self, base): self.base, self.session = base, requests.Session()
    def call(self, method, path, body=None, key=None, expect=None, headers=None, raw=False):
        h = {'Idempotency-Key': key or str(uuid.uuid4()), **(headers or {})}
        response = self.session.request(method, self.base + '/api/v1' + path, json=body, headers=h, timeout=35)
        data = response.text if raw else response.json()
        if expect is not None: assert response.status_code == expect, (method, path, response.status_code, data)
        return response.status_code, data
    def get(self, path, **kw): return self.call('GET', path, **kw)[1]
    def post(self, path, body=None, **kw): return self.call('POST', path, body, **kw)[1]
    def login(self, email): self.post('/auth/login', {'email':email,'password':'EventHub!2026'}, expect=200); return self
    def register(self, i):
        self.post('/auth/register', {'name':f'Test fan {i}','email':f'fan-{i}@test.example','password':'Integration!2026'}, expect=201)
        return self

def main():
    with tempfile.TemporaryDirectory(prefix='eventhub-tests-') as tmp:
        dbpath = pathlib.Path(tmp) / 'test.db'
        with socket.socket() as sock: sock.bind(('127.0.0.1',0)); port=sock.getsockname()[1]
        base = f'http://127.0.0.1:{port}'
        env = {**os.environ, 'ASPNETCORE_ENVIRONMENT':'Development', 'ASPNETCORE_URLS':base,
               'ConnectionStrings__Database':f'Data Source={dbpath};Default Timeout=20',
               'TicketSigningKey':'integration-only-signing-key-at-least-32-characters',
               'Payments__StripeWebhookSecret':'whsec_integration_only',
               'Booking__AdmissionCapacity':'1','Booking__AdmissionSeconds':'600',
               'Logging__LogLevel__Default':'Warning'}
        if sql_context:
            env['DatabaseProvider']='SqlServer';env['ConnectionStrings__Database']=sql_context.connection
        log = open(pathlib.Path(tmp)/'api.log','w+')
        process = subprocess.Popen([DOTNET, 'run','--project',str(ROOT/'backend/EventHub.Api'),'--no-build','--no-launch-profile'],env=env,cwd=ROOT,stdout=log,stderr=log)
        try:
            for _ in range(160):
                if process.poll() is not None: raise RuntimeError('API exited before startup')
                try:
                    if requests.get(base+'/health',timeout=1).status_code==200: break
                except requests.RequestException: pass
                time.sleep(.25)
            else: raise RuntimeError('API startup timed out')
            anonymous = Client(base)
            customer = Client(base).login('maya@example.com')
            organizer = Client(base).login('host@eventhub.test')
            admin = Client(base).login('admin@eventhub.test')
            staff = Client(base).login('gate@eventhub.test')
            check(anonymous.get('/config')['paymentProvider']=='Sandbox','explicit sandbox configuration')
            catalog=anonymous.get('/events')
            check(catalog['total']==8,'seeded catalog and venue join')
            check(all(x['ev']['category']=='Music' for x in anonymous.get('/events?category=Music')['items']),'category search filters')
            check(anonymous.get('/events?city=Chattogram')['total']==2,'city filters')
            check(anonymous.get('/events?q=After%20Dark')['total']==1,'full catalog text search')
            check(anonymous.get('/events?maxPrice=100000')['total']==3,'minor-unit price filter')
            anonymous.call('POST','/holds',{'eventId':1,'seatIds':[1]},expect=401)
            customer.call('GET','/admin/users',expect=403)
            customer.call('POST','/holds',{'eventId':1,'seatIds':[1,1]},expect=400)
            anonymous.call('POST','/auth/login',{'email':None,'password':'test'},expect=400)
            check(True,'authentication, role, and malformed input boundaries')
            fans=[Client(base).register(i) for i in range(16)]
            def hold(client,seats,event=1,key=None): return client.call('POST','/holds',{'eventId':event,'seatIds':seats},key=key)
            with futures.ThreadPoolExecutor(max_workers=16) as pool: results=list(pool.map(lambda c:hold(c,[1]),fans))
            winners=[i for i,r in enumerate(results) if r[0]==201]
            check(len(winners)==1 and all(r[0] in (201,409) for r in results),'16 independent users race for one seat: exactly one winner')
            with open_db(dbpath) as db:
                check(db.execute('SELECT COUNT(*) FROM Seats WHERE EventId=1 AND Id=1 AND Status="HELD"').fetchone()[0]==1,'database confirms one authoritative seat owner')
            winner=winners[0];h=results[winner][1]
            fans[(winner+1)%16].call('DELETE','/holds/'+h['id'],expect=404)
            fans[winner].call('DELETE','/holds/'+h['id'],expect=200)
            check(True,'hold ownership and explicit release')
            with futures.ThreadPoolExecutor(max_workers=2) as pool:
                a,b=list(pool.map(lambda pair:hold(pair[0],pair[1]),[(fans[0],[1,2]),(fans[1],[2,3])]))
            check(sorted([a[0],b[0]])==[201,409],'overlapping seat groups succeed or fail atomically')
            loser_unique=3 if a[0]==201 else 1
            with open_db(dbpath) as db: check(db.execute('SELECT Status FROM Seats WHERE EventId=1 AND Id=?',(loser_unique,)).fetchone()[0]=='AVAILABLE','losing group leaves no partial allocation')
            owner=fans[0] if a[0]==201 else fans[1];group=a[1] if a[0]==201 else b[1]
            owner.call('DELETE','/holds/'+group['id'],expect=200)
            key='repeatable-hold'
            status,h=hold(customer,[10,11],key=key)
            same=hold(customer,[10,11],key=key)[1]
            check(status==201 and same['id']==h['id'],'persistent idempotency replays the same hold')
            hold_status,_=hold(customer,[12],key=key)
            check(hold_status==409,'idempotency rejects a changed request fingerprint')
            check(h['total']==h['subtotal']+h['fee'] and h['subtotal']==600000,'server-generated immutable quote and integer fees')
            order=customer.post('/orders',{'holdId':h['id']},key='same-order',expect=201)
            check(customer.post('/orders',{'holdId':h['id']},key='same-order',expect=201)['id']==order['id'],'order creation is retry-safe')
            with futures.ThreadPoolExecutor(max_workers=2) as pool:
                payments=list(pool.map(lambda _:customer.post('/payments/'+order['id']+'/authorize',{'outcome':'success'},key='same-payment',expect=200),range(2)))
            check(all(p['order']['status']=='CONFIRMED' for p in payments),'concurrent payment retries converge on one confirmed order')
            with open_db(dbpath) as db:
                check(db.execute('SELECT COUNT(*) FROM Tickets WHERE OrderId=? AND IsActive=1',(order['id'],)).fetchone()[0]==2,'one active ticket per sold seat after retries')
                check(db.execute('SELECT COUNT(*) FROM Payments WHERE OrderId=?',(order['id'],)).fetchone()[0]==1,'one persistent payment attempt per order')
            tickets=customer.get('/tickets/me');ticket=tickets[0]
            customer.call('GET','/tickets/'+ticket['ticket']['id']+'/qr',raw=True,expect=200)
            check('BEGIN:VCALENDAR' in customer.call('GET','/tickets/'+ticket['ticket']['id']+'/calendar',raw=True,expect=200)[1],'signed QR ticket and downloadable calendar')
            check('Booking receipt' in customer.call('GET','/orders/'+order['id']+'/invoice',raw=True,expect=200)[1],'owner-authorized printable invoice')
            fans[4].call('GET','/orders/'+order['id'],expect=404)
            fans[4].call('GET','/tickets/'+ticket['ticket']['id']+'/qr',raw=True,expect=404)
            check(True,'order and QR private ownership checks')
            event_reference='sandbox_'+order['id']
            raw=json.dumps({'id':'evt_duplicate','data':{'object':{'id':order['id']}}},separators=(',',':'))
            stamp=str(int(time.time()));signature=hmac.new(b'whsec_integration_only',(stamp+'.'+raw).encode(),hashlib.sha256).hexdigest()
            headers={'Content-Type':'application/json','Stripe-Signature':f't={stamp},v1={signature}'}
            statuses=[requests.post(base+'/api/v1/webhooks/payments',data=raw,headers=headers).status_code for _ in range(5)]
            check(all(s==200 for s in statuses),'signed duplicate webhook delivery is accepted and deduplicated')
            with open_db(dbpath) as db: check(db.execute('SELECT COUNT(*) FROM Inbox WHERE Id="evt_duplicate"').fetchone()[0]==1,'inbox records exactly one logical webhook event')
            bad=requests.post(base+'/api/v1/webhooks/payments',data=raw,headers={'Content-Type':'application/json','Stripe-Signature':f't={stamp},v1=bad'})
            check(bad.status_code==400,'forged webhook signature is rejected')
            # Expiry fixture and stale sweeper: manipulate only this isolated test database.
            _,expiring=hold(fans[2],[30])
            past=(datetime.now(timezone.utc)-timedelta(minutes=1)).strftime('%Y-%m-%d %H:%M:%S')
            with open_db(dbpath) as db:
                db.execute('UPDATE Holds SET ExpiresAt=? WHERE Id=?',(past,expiring['id']));db.execute('UPDATE Seats SET ExpiresAt=? WHERE HoldId=?',(past,expiring['id']))
            fans[2].call('POST','/orders',{'holdId':expiring['id']},expect=410)
            _,replacement=hold(fans[3],[30])
            with open_db(dbpath) as db: db.execute('UPDATE Holds SET Status="ACTIVE", ExpiresAt=? WHERE Id=?',(past,expiring['id']))
            fans[2].get('/holds/'+expiring['id'])
            with open_db(dbpath) as db: check(db.execute('SELECT HoldId FROM Seats WHERE EventId=1 AND Id=30').fetchone()[0]==replacement['id'],'expired hold is rejected and stale cleanup preserves the new owner')
            fans[3].call('DELETE','/holds/'+replacement['id'],expect=200)
            _,decline=hold(fans[4],[40]);o=fans[4].post('/orders',{'holdId':decline['id']},expect=201)
            declined=fans[4].post('/payments/'+o['id']+'/authorize',{'outcome':'decline'},expect=200)
            check(declined['order']['status']=='FAILED' and not declined['tickets'],'declined payment does not issue tickets')
            with open_db(dbpath) as db: check(db.execute('SELECT Status FROM Seats WHERE EventId=1 AND Id=40').fetchone()[0]=='AVAILABLE','definitive payment failure safely releases seats')
            _,delayed=hold(fans[5],[41]);o=fans[5].post('/orders',{'holdId':delayed['id']},expect=201)
            result=fans[5].post('/payments/'+o['id']+'/authorize',{'outcome':'delayed'},expect=200)
            check(result['order']['status']=='PROCESSING','ambiguous capture remains processing')
            with open_db(dbpath) as db: db.execute('UPDATE Holds SET ExpiresAt=? WHERE Id=?',(past,delayed['id']))
            time.sleep(2.2)
            with open_db(dbpath) as db: check(db.execute('SELECT Status FROM Seats WHERE EventId=1 AND Id=41').fetchone()[0]=='ALLOCATED','unknown capture retains allocated inventory beyond hold TTL')
            for _ in range(24):
                latest=fans[5].get('/orders/'+o['id'])
                if latest['order']['status']=='CONFIRMED':break
                time.sleep(.5)
            check(latest['order']['status']=='CONFIRMED' and len(latest['tickets'])==1,'background reconciliation recovers a delayed capture without duplicate tickets')
            refund=customer.post('/orders/'+order['id']+'/refund',{'reason':'Unable to attend this evening'},expect=200)
            customer.call('POST','/tickets/'+ticket['ticket']['id']+'/transfer',{'email':'fan-6@test.example'},expect=409)
            approved=admin.post('/admin/refunds/'+refund['id']+'/review',{'approve':True},expect=200)
            check(approved['status']=='APPROVED' and customer.get('/orders/'+order['id'])['order']['status']=='REFUNDED','refund approval settles provider, revokes tickets, and updates order')
            with open_db(dbpath) as db: check(db.execute('SELECT COUNT(*) FROM Tickets WHERE OrderId=? AND IsActive=1',(order['id'],)).fetchone()[0]==0,'refund removes active ticket allocations')
            # A future event inside the venue-entry window.
            start=(datetime.now(timezone.utc)+timedelta(minutes=20)).isoformat();end=(datetime.now(timezone.utc)+timedelta(hours=3)).isoformat()
            event=organizer.post('/organizer/events',{'title':'Integration Live','artist':'Test ensemble','category':'Music','description':'A real fixture for checking the complete ticket entry workflow.','venueId':1,'startsAt':start,'endsAt':end,'minPrice':100000,'image':'/images/concert.jpg','queueEnabled':True,'featured':False,'status':'PUBLISHED'},expect=201)
            event_id=event['id']
            fans[7].call('POST','/holds',{'eventId':event_id,'seatIds':[1]},expect=403)
            queue=fans[7].post(f'/events/{event_id}/queue',expect=200)
            next_queue=fans[8].post(f'/events/{event_id}/queue',expect=200)
            check(queue['status']=='ADMITTED' and next_queue['status']=='WAITING' and next_queue['position']==1,'server-enforced FIFO waiting room and admission capacity')
            _,h=fans[7].call('POST','/holds',{'eventId':event_id,'seatIds':[1],'admissionToken':queue['admissionToken']},expect=201)
            o=fans[7].post('/orders',{'holdId':h['id']},expect=201)
            fans[7].post('/payments/'+o['id']+'/authorize',{'outcome':'success'},expect=200)
            before=fans[7].get('/tickets/me')[0]
            fans[7].post('/tickets/'+before['ticket']['id']+'/transfer',{'email':'fan-9@test.example'},expect=200)
            staff.call('POST','/tickets/check-in',{'eventId':event_id,'gate':'North','token':before['token']},expect=409)
            transferred=fans[9].get('/tickets/me')[0]
            check(before['token']!=transferred['token'] and transferred['ticket']['transferCount']==1,'ticket transfer changes ownership and invalidates the old signed token')
            fans[9].call('POST','/tickets/'+transferred['ticket']['id']+'/transfer',{'email':'fan-10@test.example'},expect=409)
            with futures.ThreadPoolExecutor(max_workers=2) as pool:
                scans=list(pool.map(lambda _:staff.call('POST','/tickets/check-in',{'eventId':event_id,'gate':'North','token':transferred['token']}),range(2)))
            check(sorted([s[0] for s in scans])==[200,409],'two concurrent gate scans permit exactly one entry')
            with open_db(dbpath) as db: check(db.execute('SELECT COUNT(*) FROM Scans WHERE TicketId=?',(transferred['ticket']['id'],)).fetchone()[0]==1,'database verifies one venue entry record')
            overview=organizer.get('/organizer/overview')
            check(overview['ticketsSold']>=2 and overview['revenue']>0,'organizer analytics come from persisted bookings')
            customer.call('PUT','/favorites/2',expect=200);check(2 in customer.get('/favorites'),'saved events persist per account')
            check(len(customer.get('/notifications'))>=2,'booking and refund notifications persist')
            check(len(admin.get('/admin/overview')['audit'])>0,'administrative audit history records actual actions')
            print(f'\n{checks} integration checks passed against isolated {'SQL Server' if sql_context else 'SQLite'}.\n',flush=True)
        except Exception:
            log.flush();log.seek(0);print('\nAPI log tail:\n'+log.read()[-12000:],flush=True)
            raise
        finally:
            process.terminate()
            try:process.wait(timeout=8)
            except subprocess.TimeoutExpired:process.kill();process.wait()
            log.close()

if __name__=='__main__':
    if '--sqlserver' in sys.argv:
        with SqlFixture() as fixture:
            sql_context=fixture
            main()
    else:main()
