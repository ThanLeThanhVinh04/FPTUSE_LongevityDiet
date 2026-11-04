import test from 'node:test'
import assert from 'node:assert/strict'
import worker from '../src/index.mjs'

class FakeDB {
  constructor() { this.rows = new Map(); this.audit = new Map() }
  prepare(sql) {
    const db = this
    return { bind(...args) {
      return {
        async run() { return db.run(sql, args) },
        async first() { return db.first(sql, args) },
        sql, args
      }
    } }
  }
  async first(sql, args) {
    if (!sql.startsWith('SELECT id,user_id,family_id,')) throw new Error('Unexpected SELECT')
    const row = Array.from(this.rows.values()).find(r => r.token_hash === args[0])
    if (!row) return null
    return { id: row.id, user_id: row.user_id, family_id: row.family_id, created_at: row.created_at, expires_at: row.expires_at, revoked_at: row.revoked_at }
  }
  async run(sql, args) {
    if (sql.startsWith('INSERT INTO refresh_sessions (id,user_id,token_hash,family_id') && !sql.includes(' SELECT ')) {
      const [id,user_id,token_hash,family_id,created_at,expires_at] = args
      if (this.rows.has(id) || Array.from(this.rows.values()).some(x => x.token_hash === token_hash)) throw new Error('Unique violation')
      this.rows.set(id, {id,user_id,token_hash,family_id,created_at,expires_at,revoked_at:null,replaced_by_token_id:null})
      return { success:true,meta:{changes:1}}
    }
    if (sql.startsWith('UPDATE refresh_sessions SET revoked_at=?,replaced_by_token_id=?')) {
      const [revoked,id,oldHash,now] = args
      const row = Array.from(this.rows.values()).find(x=>x.token_hash===oldHash && x.revoked_at===null && x.expires_at>now)
      if (!row) return {success:true,meta:{changes:0}}
      row.revoked_at=revoked;row.replaced_by_token_id=id
      return {success:true,meta:{changes:1}}
    }
    if (sql.startsWith('INSERT INTO refresh_sessions (id,user_id,token_hash,family_id,created_at,expires_at) SELECT')) {
      const [id,hash,created,expires,oldHash,now] = args
      const row=Array.from(this.rows.values()).find(x=>x.token_hash===oldHash && x.revoked_at===null && x.expires_at>now)
      if (!row) return {success:true,meta:{changes:0}}
      if (this.rows.has(id) || Array.from(this.rows.values()).some(x=>x.token_hash===hash)) throw new Error('Unique violation')
      this.rows.set(id,{id,user_id:row.user_id,token_hash:hash,family_id:row.family_id,created_at:created,expires_at:expires,revoked_at:null,replaced_by_token_id:null})
      return {success:true,meta:{changes:1}}
    }
    if (sql.startsWith('UPDATE refresh_sessions SET revoked_at=? WHERE token_hash=')) {
      const [now,hash] = args
      const row=Array.from(this.rows.values()).find(x=>x.token_hash===hash && x.revoked_at===null)
      if(row){row.revoked_at=now;return {success:true,meta:{changes:1}}}
      return {success:true,meta:{changes:0}}
    }
    if (sql.startsWith('DELETE FROM refresh_sessions WHERE expires_at <')) {
      let changes=0
      for(const [id,row] of this.rows){if(row.expires_at<args[0]){this.rows.delete(id); changes++}}
      return {success:true,meta:{changes}}
    }
    throw new Error('Unexpected SQL: '+sql)
  }
  async batch(statements) {
    // Emulate D1 atomic serialization for concurrent Worker calls.
    const previous=this.batchQueue||Promise.resolve()
    let release
    this.batchQueue=new Promise(resolve=>{release=resolve})
    await previous
    const backup=new Map(Array.from(this.rows.entries()).map(([k,v])=>[k,{...v}]))
    try {
      const result=[]
      for(const item of statements) result.push(await this.run(item.sql,item.args))
      return result
    } catch (err){this.rows=backup;throw err}
    finally {release()}
  }
}
const ids={
  user:'123e4567-e89b-42d3-a456-426614174000',
  first:'123e4567-e89b-42d3-a456-426614174001',
  next:'123e4567-e89b-42d3-a456-426614174002',
  concurrent:'123e4567-e89b-42d3-a456-426614174003',
  family:'123e4567-e89b-42d3-a456-426614174004'
}
const hashes={first:'A'.repeat(64),next:'B'.repeat(64),concurrent:'C'.repeat(64)}
const apiKey='testing-secret-value'
function env() {return {DB:new FakeDB(),SERVICE_BEARER:apiKey}}
async function post(where, state, body, auth=apiKey) {
  const res=await worker.fetch(new Request('https://fake.example'+where,{
    method:'POST',
    headers:{'Content-Type':'application/json','Authorization':'Bearer '+auth},
    body:JSON.stringify(body)}),state)
  return {status:res.status,body:await res.json()}
}
function session(start=ids.first,hash=hashes.first){
  const t=Math.floor(Date.now()/1000)
  return {id:start,userId:ids.user,tokenHash:hash,familyId:ids.family,createdAt:t,expiresAt:t+3600}
}
test('non-POST not allowed and bearer authentication required', async()=>{
  const e=env()
  const get=await worker.fetch(new Request('https://fake.example/v1/sessions/lookup',{method:'GET'}),e)
  assert.equal(get.status,405)
  assert.equal((await post('/v1/sessions/issue',e,session(),'bad')).status,401)
  assert.equal(e.DB.rows.size,0)
})
test('issue stores only token hash and allows valid lookup', async()=>{
  const e=env();const b=session()
  const created=await post('/v1/sessions/issue',e,b)
  assert.equal(created.status,201)
  const lookup=await post('/v1/sessions/lookup',e,{tokenHash:b.tokenHash})
  assert.equal(lookup.status,200)
  assert.equal(lookup.body.userId,ids.user)
  assert.equal(e.DB.rows.get(ids.first).token_hash,b.tokenHash)
  assert.equal('rawRefreshToken' in e.DB.rows.get(ids.first),false)
  assert.equal(JSON.stringify(lookup.body).includes(b.tokenHash),false)
})
test('hash format is checked and expired/absent hash is rejected', async()=>{
  const e=env()
  assert.equal((await post('/v1/sessions/issue',e,{...session(),tokenHash:'nope'})).status,400)
  assert.equal((await post('/v1/sessions/lookup',e,{tokenHash:hashes.next})).status,401)
  const row=session();row.expiresAt=Math.floor(Date.now()/1000)+1
  await post('/v1/sessions/issue',e,row)
  e.DB.rows.get(ids.first).expires_at=Math.floor(Date.now()/1000)-1
  assert.equal((await post('/v1/sessions/lookup',e,{tokenHash:hashes.first})).status,401)
})
test('refresh rotation invalidates old hash, keeps one family, and stops replay', async()=>{
  const e=env();await post('/v1/sessions/issue',e,session())
  const expiresAt=Math.floor(Date.now()/1000)+3600
  const b={oldTokenHash:hashes.first,newTokenHash:hashes.next,newId:ids.next,expiresAt}
  const rotated=await post('/v1/sessions/rotate',e,b)
  assert.equal(rotated.status,200)
  assert.equal((await post('/v1/sessions/lookup',e,{tokenHash:hashes.first})).status,401)
  assert.equal((await post('/v1/sessions/lookup',e,{tokenHash:hashes.next})).status,200)
  assert.equal(e.DB.rows.get(ids.next).family_id,ids.family)
  assert.equal((await post('/v1/sessions/rotate',e,b)).status,401)
  assert.equal(e.DB.rows.size,2)
})
test('concurrent rotations: single-use token has exactly one winner', async()=>{
  const e=env();await post('/v1/sessions/issue',e,session())
  const expiresAt=Math.floor(Date.now()/1000)+3600
  const first={oldTokenHash:hashes.first,newTokenHash:hashes.next,newId:ids.next,expiresAt}
  const second={oldTokenHash:hashes.first,newTokenHash:hashes.concurrent,newId:ids.concurrent,expiresAt}
  // Simulates serializable D1 batch order; does not replace an actual live D1 stress test.
  const responses=await Promise.all([post('/v1/sessions/rotate',e,first),post('/v1/sessions/rotate',e,second)])
  assert.deepEqual(responses.map(x=>x.status).sort(),[200,401])
  assert.equal(e.DB.rows.size,2)
})
test('revocation is idempotent and blocks lookup', async()=>{
  const e=env();await post('/v1/sessions/issue',e,session())
  assert.equal((await post('/v1/sessions/revoke',e,{tokenHash:hashes.first})).status,200)
  assert.equal((await post('/v1/sessions/revoke',e,{tokenHash:hashes.first})).status,200)
  assert.equal((await post('/v1/sessions/lookup',e,{tokenHash:hashes.first})).status,401)
})
