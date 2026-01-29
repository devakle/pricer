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

/**
 * @typedef {Object} ExternalMoney
 * @property {string} currency
 * @property {number} amount
 */

/**
 * @typedef {Object} ExternalInstallmentInfo
 * @property {number | null} quantity
 * @property {ExternalMoney | null} amountPerInstallment
 * @property {boolean | null} interestFree
 */

/**
 * @typedef {Object} ExternalShippingInfo
 * @property {boolean | null} freeShipping
 * @property {string | null} shippingMode
 * @property {string | null} deliveryPromise
 * @property {boolean | null} pickupAvailable
 */

/**
 * @typedef {Object} ExternalSellerInfo
 * @property {string | null} sellerId
 * @property {string | null} name
 * @property {boolean | null} officialStore
 * @property {string | null} sellerType
 * @property {string | null} reputationLevel
 * @property {string | null} badges
 */

/**
 * @typedef {Object} ExternalProductOffer
 * @property {ExternalMoney | null} price
 * @property {ExternalMoney | null} originalPrice
 * @property {number | null} discountPercent
 * @property {ExternalInstallmentInfo | null} installments
 * @property {string | null} pricePerUnit
 * @property {string[] | null} paymentBadges
 */

/**
 * @typedef {Object} ExternalProductMedia
 * @property {string | null} thumbnailUrl
 * @property {string[] | null} imageUrls
 * @property {string[] | null} videoUrls
 */

/**
 * @typedef {Object} ExternalProductDto
 * @property {string | null} id
 * @property {string} title
 * @property {string} permalink
 * @property {string | null} canonicalUrl
 * @property {string[] | null} categoryPath
 * @property {string | null} searchQuery
 * @property {number | null} position
 * @property {string | null} condition
 * @property {string | null} availability
 * @property {number | null} soldQuantity
 * @property {string | null} lastUpdated
 * @property {ExternalProductOffer} offer
 * @property {ExternalShippingInfo} shipping
 * @property {ExternalSellerInfo} seller
 * @property {ExternalProductMedia} media
 * @property {Object.<string, string> | null} attributes
 * @property {string} provider
 * @property {string} source
 * @property {string | null} scrapeProvider
 * @property {string | null} htmlVersion
 * @property {string | null} selectorVersion
 * @property {string} fetchedAtUtc
 * @property {string[] | null} warnings
 * @property {string | null} location
 */
