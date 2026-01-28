/**
 * @typedef {Object} ApiError
 * @property {string} code
 * @property {string} message
 */

/**
 * @template T
 * @typedef {Object} ApiResponse
 * @property {boolean} ok
 * @property {T | null} data
 * @property {ApiError | null} error
 */

/**
 * @typedef {Object} StoreNearDto
 * @property {string} storeId
 * @property {string} name
 * @property {string | null} chainName
 * @property {string | null} address
 * @property {string | null} city
 * @property {number} lat
 * @property {number} lng
 * @property {number} distanceMeters
 */

/**
 * @typedef {Object} CreatePriceReportRequest
 * @property {string} storeId
 * @property {string} skuId
 * @property {number} price
 * @property {string} currency
 * @property {string} source
 * @property {string | null} evidenceUrl
 */

/**
 * @typedef {Object} CreatePriceReportResponse
 * @property {string} reportId
 * @property {string} reportedAt
 */

/**
 * @typedef {Object} AdminLoginRequest
 * @property {string} username
 * @property {string} password
 */

/**
 * @typedef {Object} AdminLoginResponse
 * @property {string} token
 * @property {string} expiresAt
 */

/**
 * @typedef {Object} SkuSearchDto
 * @property {string} skuId
 * @property {string} displayName
 * @property {string} productName
 * @property {string | null} brand
 * @property {number | null} sizeValue
 * @property {string | null} sizeUnit
 */

/**
 * @typedef {Object} PriceReportNearDto
 * @property {string} storeId
 * @property {string} storeName
 * @property {number} lat
 * @property {number} lng
 * @property {number} price
 * @property {string} currency
 * @property {string} reportedAt
 */

/**
 * @typedef {Object} ProductSearchDto
 * @property {string} productId
 * @property {string} name
 * @property {string | null} brand
 * @property {string | null} category
 */

/**
 * @typedef {Object} CreateProductResponse
 * @property {string} productId
 * @property {string} skuId
 * @property {string | null} imageUrl
 * @property {string | null} priceReportId
 */

/**
 * @typedef {Object} StoreProductDto
 * @property {string} skuId
 * @property {string} skuDisplayName
 * @property {string} productName
 * @property {string | null} brand
 * @property {string | null} imageUrl
 * @property {number} price
 * @property {string} currency
 * @property {string} reportedAt
 */

/**
 * @typedef {Object} GetStoresNearParams
 * @property {number} lat
 * @property {number} lng
 * @property {number} radiusKm
 * @property {number} take
 */
