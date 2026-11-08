// A website to monitor whose health a test can switch. It stands in for "the customer's site" in the end-to-end run.
//
//   GET  /health          200 while up, 503 while down
//   POST /control/down    start failing
//   POST /control/up      recover
//   GET  /control/state   {"up": true}
import { createServer } from 'node:http'

const port = Number(process.env.TARGET_PORT ?? 4010)
let up = true

const send = (response, status, body, type = 'text/plain') => {
  response.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store' })
  response.end(body)
}

createServer((request, response) => {
  const { pathname } = new URL(request.url ?? '/', 'http://target')

  if (pathname === '/health') return send(response, up ? 200 : 503, up ? 'ok' : 'down')
  if (pathname === '/control/state') return send(response, 200, JSON.stringify({ up }), 'application/json')
  if (request.method === 'POST' && pathname === '/control/down') {
    up = false
    return send(response, 204, '')
  }
  if (request.method === 'POST' && pathname === '/control/up') {
    up = true
    return send(response, 204, '')
  }
  return send(response, 404, 'not found')
}).listen(port, '0.0.0.0', () => console.log(`target listening on ${port}`))
