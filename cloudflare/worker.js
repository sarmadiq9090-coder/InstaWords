// Serves the static tafqit site under https://fastkiwi.app/tafqit/ (Workers Static Assets).
// Only requests matching the route "fastkiwi.app/tafqit*" reach this Worker; the rest of the
// domain is untouched.
const BASE = "/tafqit";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === BASE) {
      return Response.redirect(`${url.origin}${BASE}/${url.search}`, 301);
    }
    if (!url.pathname.startsWith(BASE + "/")) {
      return new Response("Not found", { status: 404 });
    }
    const res = await env.ASSETS.fetch(request);
    // The service worker must always be revalidated so updates reach offline users.
    if (url.pathname.endsWith("/sw.js")) {
      const h = new Headers(res.headers);
      h.set("Cache-Control", "no-cache");
      return new Response(res.body, { status: res.status, headers: h });
    }
    return res;
  },
};
