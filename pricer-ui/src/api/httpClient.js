export class HttpClient {
  /**
   * @param {{ baseUrl: string, getToken?: (() => string | null) }} options
   */
  constructor(options) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, '');
    this.getToken = options.getToken || (() => null);
  }

  /**
   * @param {string} path
   * @param {{ query?: Record<string, string | number | boolean | undefined> }} [options]
   */
  async get(path, options = {}) {
    return this.#request('GET', path, { query: options.query });
  }

  /**
   * @param {string} path
   * @param {unknown} body
   */
  async post(path, body) {
    return this.#request('POST', path, { body });
  }

  /**
   * @param {string} path
   * @param {FormData} body
   */
  async postForm(path, body) {
    return this.#request('POST', path, { body, isForm: true });
  }

  async #request(method, path, { query, body, isForm } = {}) {
    const url = new URL(this.baseUrl + path);
    if (query) {
      Object.entries(query).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== '') {
          url.searchParams.set(key, String(value));
        }
      });
    }

    const headers = {
      Accept: 'application/json',
    };

    const token = this.getToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    if (body !== undefined && !isForm) {
      headers['Content-Type'] = 'application/json';
    }

    const response = await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    });

    const contentType = response.headers.get('content-type') || '';
    if (!contentType.includes('application/json')) {
      return {
        ok: false,
        status: response.status,
        error: { code: 'unexpected', message: 'Respuesta no JSON del servidor.' },
        data: null,
      };
    }

    const payload = await response.json();
    return {
      ...payload,
      status: response.status,
    };
  }
}
