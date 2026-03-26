// Hàm Toggle Password (ẩn/hiện mật khẩu)
function togglePassword(inputId, el) {
    const input = document.getElementById(inputId);
    const icon = el.querySelector("i");
    if (input.type === "password") {
        input.type = "text";
        icon.classList.remove("fa-eye-slash");
        icon.classList.add("fa-eye");
    } else {
        input.type = "password";
        icon.classList.remove("fa-eye");
        icon.classList.add("fa-eye-slash");
    }
}

// 1. Hàm cập nhật Sidebar
function updateCartSidebar() {
    fetch('/Cart/GetCartJson')
        .then(res => res.json())
        .then(data => {
            const body = document.getElementById('cartSidebarBody');
            const total = document.getElementById('cartSidebarTotal');

            body.innerHTML = '';

            if (!data.items || data.items.length === 0) {
                body.innerHTML = '<div class="d-flex flex-column align-items-center justify-content-center h-100"><p class="text-muted small">Your cart is empty.</p></div>';
                total.innerText = '0 VND';
            } else {
                data.items.forEach(item => {
                    const priceFormatted = (item.price * 1000).toLocaleString('en-US') + " VND";

                    const html = `
                        <div class="cart-item">
                            <a href="/Product/Detail/${item.productId}">
                                <img src="${item.productImage}" alt="${item.productName}" class="cart-item-img">
                            </a>
                            
                            <div class="cart-item-info">
                                <div class="d-flex justify-content-between align-items-start">
                                    <a href="/Product/Detail/${item.productId}" class="cart-item-name text-truncate" style="max-width: 130px; color: #000;">${item.productName}</a>
                                    
                                    <button class="btn-remove-item border-0 bg-transparent p-0" 
                                            style="color: #000 !important; font-size: 16px;"
                                            onclick="window.removeFromSidebar(${item.productId}, '${item.size}')">
                                        <i class="fa-solid fa-xmark"></i>
                                    </button>
                                </div>

                                <div class="cart-item-variant text-muted small mb-2">Size: ${item.size}</div>

                                <div class="cart-item-bottom">
                                    <span class="cart-item-price" style="color: #000; font-weight: bold;">${priceFormatted}</span>

                                    <div class="qty-control-sm">
                                        <button onclick="window.updateCartQty(${item.productId}, '${item.size}', -1)">-</button>
                                        <span>${item.quantity}</span>
                                        <button onclick="window.updateCartQty(${item.productId}, '${item.size}', 1)">+</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    `;
                    body.insertAdjacentHTML('beforeend', html);
                });
                total.innerText = (data.total * 1000).toLocaleString('en-US') + " VND";
            }

            if (typeof window.updateCartBadgeGlobal === "function") {
                window.updateCartBadgeGlobal();
            }

            // Mở Sidebar
            const myOffcanvas = document.getElementById('cartSidebar');
            if (window.bootstrap) {
                const bsOffcanvas = bootstrap.Offcanvas.getOrCreateInstance(myOffcanvas);
                bsOffcanvas.show();
            }
        })
        .catch(err => console.error("Lỗi tải giỏ hàng:", err));
}

// 2. Hàm Xóa 
window.removeFromSidebar = function (id, size) {
    fetch(`/Cart/Remove?id=${id}&size=${size}`, { method: 'POST' })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                updateCartSidebar();
            } else {
                alert("Error: " + (data.msg || "Failed to remove item"));
            }
        })
        .catch(err => {
            console.error("Remove error:", err);
            alert("An error occurred while removing the item.");
        });
};

// 3. Hàm Tăng/Giảm Số Lượng
window.updateCartQty = function (id, size, change) {
    fetch(`/Cart/UpdateQuantity?id=${id}&size=${size}&change=${change}`, { method: 'POST' })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                updateCartSidebar();
            } else {
                alert(data.msg);
            }
        })
        .catch(err => console.error(err));
};


// ==========================================
// PHẦN 2: LOGIC MỚI (QUICK VIEW & BUY NOW)
// ==========================================

document.addEventListener("DOMContentLoaded", function () {
    const ALL_SIZES = ['S', 'M', 'L', 'XL'];
    let currentProductSizes = [];

    // --- A. BẮT SỰ KIỆN CLICK TOÀN TRANG (Event Delegation) ---
    document.body.addEventListener('click', function (e) {
        // 1. Logic cho nút "ADD TO CART"
        const btnAdd = e.target.closest('.btn-quick-view');
        if (btnAdd) {
            e.preventDefault();
            const id = btnAdd.getAttribute('data-id');
            openQuickView(id, false);
        }

        // 2. Logic cho nút "MUA NGAY"
        const btnBuyNow = e.target.closest('.btn-buy-now-trigger');
        if (btnBuyNow) {
            e.preventDefault();
            const id = btnBuyNow.getAttribute('data-id');
            openQuickView(id, true);
        }
    });

    // --- B. HÀM MỞ POPUP (Xử lý giao diện Modal) ---
    function openQuickView(id, isBuyNowMode) {
        const modalEl = document.getElementById('quickViewModal');
        const modalBody = document.getElementById('quickViewContent');

        if (!modalEl) return;

        modalBody.innerHTML = '<div class="text-center py-5 w-100"><div class="spinner-border text-dark"></div></div>';
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        fetch(`/Product/GetProductJson?id=${id}`)
            .then(res => res.json())
            .then(data => {
                currentProductSizes = data.sizes || [];

                // 👉 ĐÃ FIX: Logic xử lý vẽ nút Size (CÓ QUẢN LÝ TÚI/NÓN)
                let sizeHtml = '';
                let sizeHeaderHtml = '';

                if (data.categoryId == 3) {
                    // Categoty Accessories (ID = 3) -> Chế độ ONE SIZE
                    sizeHeaderHtml = `<label class="fw-bold small text-uppercase text-muted mb-0">Size</label>`;
                    sizeHtml = `<div class="btn-size-qv active bg-dark text-white px-3" style="width: auto;">ONE SIZE</div>`;

                    // Gán tự động ở background
                    setTimeout(() => {
                        const firstAvailable = currentProductSizes.find(x => x.qty > 0);
                        const hiddenSizeVal = firstAvailable ? firstAvailable.name : 'One Size';
                        document.getElementById('qvSelectedSize').value = hiddenSizeVal;
                        document.getElementById('qvCurrentMaxStock').value = data.stock;
                    }, 100);
                } else {
                    // Quần Áo Bình Thường -> Hiện 4 size
                    sizeHeaderHtml = `
                        <label class="fw-bold small text-uppercase text-muted mb-0">Size</label>
                        <a href="javascript:void(0)" 
                           class="text-decoration-underline text-muted small fw-bold text-uppercase size-guide-link" 
                           onclick="showSizeGuide(${data.categoryId})"
                           style="letter-spacing: 1px; font-size: 11px;">
                           Size Guide
                        </a>
                    `;

                    ALL_SIZES.forEach(s => {
                        const sizeData = currentProductSizes.find(x => x.name === s);
                        if (sizeData && sizeData.qty > 0) {
                            sizeHtml += `<div class="btn-size-qv" onclick="selectSizeGlobal(this, '${s}', ${sizeData.qty})">${s}</div>`;
                        } else {
                            sizeHtml += `<div class="btn-size-qv disabled text-decoration-line-through text-muted" style="cursor: not-allowed;">${s}</div>`;
                        }
                    });
                }

                // Nút bấm
                let actionButtonHtml = '';
                if (isBuyNowMode) {
                    actionButtonHtml = `<button class="btn-add-cart-qv w-100" onclick="processCheckoutGlobal(${data.id})">CHECKOUT</button>`;
                } else {
                    actionButtonHtml = `<button class="btn-add-cart-qv w-100" onclick="addToCartGlobal(${data.id})">ADD TO CART</button>`;
                }

                // Vẽ HTML vào Modal
                modalBody.innerHTML = `
                <div class="row g-0">
                    <div class="col-md-6">
                        <div class="quick-view-img-box">
                            <img src="${data.image}" alt="${data.name}" style="width: 100%; height: 100%; object-fit: cover;">
                        </div>
                    </div>

                    <div class="col-md-6 p-4 d-flex flex-column text-start">
                        <button type="button" class="btn-close ms-auto mb-2" data-bs-dismiss="modal"></button>
                    
                        <h4 class="fw-bold text-uppercase mb-1" style="font-size: 1.4rem;">${data.name}</h4>
                        <div class="fs-4 fw-bold mb-1 text-danger">${(data.price * 1000).toLocaleString()} VND</div>
                    
                        <div class="text-muted small mb-4 fw-bold" id="qvStockDisplay">Stock: ${data.stock}</div>
                    
                        <div class="d-flex justify-content-between align-items-center mb-2">
                            ${sizeHeaderHtml} 
                        </div>

                        <div class="mb-4">
                            <div class="d-flex gap-2">
                                ${sizeHtml}
                                <input type="hidden" id="qvSelectedSize">
                                <input type="hidden" id="qvCurrentMaxStock" value="${data.stock}">
                            </div>
                            <div id="qvSizeError" class="text-danger small fw-bold mt-2" style="display:none;">
                                <i class="fa-solid fa-circle-exclamation me-1"></i> Please select a size
                            </div>
                        </div>
                    
                        <div class="mb-3 d-flex justify-content-between align-items-center">
                            <label class="fw-bold small text-uppercase mb-0" style="padding-top: 2px;">
                                Quantity
                            </label>
                            <div class="quantity-box">
                                <button class="btn-qty" onclick="changeQtyGlobal(-1)">-</button>
                                <input type="text" id="qvQtyInput" value="1" readonly style="outline: none;">
                                <button class="btn-qty" onclick="changeQtyGlobal(1)">+</button>
                            </div>
                        </div>

                        <div class="mt-2 w-100">
                            ${actionButtonHtml}
                        </div>
                    </div>
                </div>`;
            })
            .catch(error => {
                console.error('Error:', error);
                modalBody.innerHTML = '<div class="text-center py-5">Failed to load product details.</div>';
            });
    }

    // --- C. CÁC HÀM HỖ TRỢ ---

    // 1. Chọn Size 
    window.selectSizeGlobal = function (btn, sizeName, maxStock) {
        document.querySelectorAll('.btn-size-qv').forEach(b => b.classList.remove('active', 'bg-dark', 'text-white'));
        btn.classList.add('active', 'bg-dark', 'text-white');

        document.getElementById('qvSelectedSize').value = sizeName;
        document.getElementById('qvCurrentMaxStock').value = maxStock;

        document.getElementById('qvStockDisplay').innerHTML = `Stock (Size ${sizeName}): <span class="text-success">${maxStock}</span>`;

        const qtyInput = document.getElementById('qvQtyInput');
        if (parseInt(qtyInput.value) > maxStock) {
            qtyInput.value = maxStock;
        }

        const errorMsg = document.getElementById('qvSizeError');
        if (errorMsg) errorMsg.style.display = 'none';
    }

    // 2. Tăng giảm số lượng 
    window.changeQtyGlobal = function (change) {
        const sizeSelected = document.getElementById('qvSelectedSize').value;
        if (!sizeSelected && change > 0) {
            alert("Vui lòng chọn Size trước!");
            return;
        }

        const input = document.getElementById('qvQtyInput');
        const maxStock = parseInt(document.getElementById('qvCurrentMaxStock').value);
        let val = parseInt(input.value) + change;

        if (val < 1) val = 1;
        if (val > maxStock) {
            alert(`Size này chỉ còn tối đa ${maxStock} sản phẩm!`);
            val = maxStock;
        }

        input.value = val;
    }

    window.showSizeGuide = function (categoryId) {
        const content = document.getElementById('sizeGuideContent');
        if (!content) return;

        let html = "";
        if (categoryId == 1) {
            html = `
            <h4 class="fw-bold mb-4 text-uppercase" style="letter-spacing: 2px;">Tops Size Guide</h4>
            <img src="/images/size-tops.jpg" alt="Tops Size Guide" class="img-fluid" />
        `;
        }
        else if (categoryId == 2) {
            html = `
            <h4 class="fw-bold mb-4 text-uppercase" style="letter-spacing: 2px;">Bottoms Size Guide</h4>
            <img src="/images/size-bottoms.jpg" alt="Bottoms Size Guide" class="img-fluid" />
        `;
        }
        else {
            html = `<p class="text-muted py-5">Size guide not available.</p>`;
        }

        content.innerHTML = html;

        const sgModalEl = document.getElementById('sizeGuideModal');
        if (sgModalEl) {
            const sgModal = bootstrap.Modal.getOrCreateInstance(sgModalEl);
            sgModal.show();
        }
    }

    // 👉 ĐÃ FIX: Thêm lệnh return để ngắt hàm nếu chưa chọn size
    function validateAndSubmit(id, callback) {
        const size = document.getElementById('qvSelectedSize').value;
        const errorMsg = document.getElementById('qvSizeError');

        if (!size) {
            if (errorMsg) {
                errorMsg.style.display = 'block';
                errorMsg.animate([
                    { transform: 'translateX(0)' }, { transform: 'translateX(-5px)' },
                    { transform: 'translateX(5px)' }, { transform: 'translateX(0)' }
                ], { duration: 300 });
            }
            return; // Dừng lại ngay lập tức
        }

        submitCartData(id, callback);
    }

    window.addToCartGlobal = function (id) {
        validateAndSubmit(id, function () {
            const modalEl = document.getElementById('quickViewModal');
            const modal = bootstrap.Modal.getInstance(modalEl);
            modal.hide();
            updateCartSidebar();
        });
    }

    //window.processCheckoutGlobal = function (id) {
    //    validateAndSubmit(id, function () {
    //        window.location.href = '/Checkout';
    //    });
    //}
    window.processCheckoutGlobal = function (id) {
        const size = document.getElementById('qvSelectedSize').value;
        const qty = document.getElementById('qvQtyInput').value;

        if (!size) {
            document.getElementById('qvSizeError').style.display = 'block';
            return;
        }
        // Bay thẳng qua Checkout, không thông qua API giỏ hàng
        window.location.href = `/Checkout?buyNowId=${id}&size=${size}&qty=${qty}`;
    }

    function submitCartData(id, onSuccess) {
        const size = document.getElementById('qvSelectedSize').value;
        const qty = document.getElementById('qvQtyInput').value;

        const formData = new FormData();
        formData.append('Id', id);
        formData.append('Size', size);
        formData.append('Quantity', qty);

        fetch('/Cart/AddToCartAjax', { method: 'POST', body: formData })
            .then(res => res.json())
            .then(data => {
                // 👉 ĐÃ FIX: CHỐT CHẶN Ở ĐÂY - Chưa đăng nhập thì bế qua trang Login
                if (data.notLoggedIn) {
                    var currentUrl = window.location.pathname + window.location.search;
                    window.location.href = '/Account/Login?ReturnUrl= ' + encodeURIComponent(currentUrl);
                    return;
                }

                if (data.success) {
                    onSuccess();
                } else {
                    // C# báo lỗi hoặc hết hàng thì báo ra đây
                    alert("Error: " + (data.msg || "Cannot add to cart!"));
                }
            })
            .catch(err => console.error(err));
    }

    window.updateCartBadgeGlobal = function () {
        fetch('/Cart/GetCartJson')
            .then(res => res.json())
            .then(data => {
                let totalQty = 0;
                if (data.items && data.items.length > 0) {
                    data.items.forEach(item => {
                        totalQty += item.quantity;
                    });
                }

                const badge = document.getElementById('cart-badge');
                if (badge) {
                    badge.innerText = totalQty;
                    badge.style.transform = 'translate(-50%, -50%) scale(1.3)';
                    setTimeout(() => {
                        badge.style.transform = 'translate(-50%, -50%) scale(1)';
                    }, 200);
                }
            })
            .catch(err => console.error("Error retrieving badge quantity:", err));
    };

    updateCartBadgeGlobal();
});