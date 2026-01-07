// Логика работы с меню и ресторанами

class MenuManager {
    constructor() {
        this.restaurants = [];
        this.dishes = [];
        this.filteredDishes = [];
        this.currentRestaurant = null;
    }

    // Загрузка ресторанов
    async loadRestaurants() {
        try {
            const response = await ApiClient.getRestaurants();

            if (response.success) {
                this.restaurants = response.data || [];
                this.displayRestaurants();
            } else {
                throw new Error(response.message);
            }
        } catch (error) {
            console.error('Error loading restaurants:', error);
            this.displayError('restaurantsGrid', 'Не удалось загрузить рестораны');

            // Заглушка для тестирования
            this.restaurants = this.getMockRestaurants();
            this.displayRestaurants();
        }
    }

    // Загрузка блюд
    async loadDishes(restaurantId = null) {
        try {
            if (restaurantId) {
                // Загружаем блюда конкретного ресторана
                const response = await ApiClient.getRestaurantDishes(restaurantId);
                if (response.success) {
                    const restaurant = this.restaurants.find(r => r.id === restaurantId);
                    this.dishes = response.data.map(dish => ({
                        ...dish,
                        restaurantId: restaurantId,
                        restaurantName: restaurant?.name || 'Ресторан'
                    }));
                }
            } else {
                // Загружаем ВСЕ блюда из ВСЕХ ресторанов
                this.dishes = [];
                
                // Сначала убедимся, что рестораны загружены
                if (this.restaurants.length === 0) {
                    await this.loadRestaurants();
                }
                
                // Для каждого ресторана загружаем его блюда
                for (const restaurant of this.restaurants) {
                    try {
                        const response = await ApiClient.getRestaurantDishes(restaurant.id);
                        if (response.success && response.data) {
                            const restaurantDishes = response.data.map(dish => ({
                                ...dish,
                                restaurantId: restaurant.id,
                                restaurantName: restaurant.name
                            }));
                            this.dishes.push(...restaurantDishes);
                        }
                    } catch (err) {
                        console.warn(`Error loading dishes for restaurant ${restaurant.id}:`, err);
                    }
                }
                
                // Если не удалось загрузить, используем заглушку
                if (this.dishes.length === 0) {
                    console.warn('No dishes loaded, using mock data');
                    this.dishes = this.getMockDishes();
                }
            }

            this.filteredDishes = [...this.dishes];
            this.displayDishes();

        } catch (error) {
            console.error('Error loading dishes:', error);
            this.displayError('dishesGrid', 'Не удалось загрузить меню');
            
            // Заглушка для тестирования
            this.dishes = this.getMockDishes();
            this.filteredDishes = [...this.dishes];
            this.displayDishes();
        }
    }

    // Отображение ресторанов
    displayRestaurants() {
        const container = document.getElementById('restaurantsGrid');
        if (!container) return;

        if (this.restaurants.length === 0) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-utensils"></i>
                    <p>Рестораны не найдены</p>
                </div>
            `;
            return;
        }

        container.innerHTML = this.restaurants.map(restaurant => `
            <div class="restaurant-card" data-id="${restaurant.id}">
                <div class="restaurant-image">
                    <img src="${restaurant.imageUrl || 'https://via.placeholder.com/300x200?text=Restaurant'}" 
                         alt="${restaurant.name}"
                         onerror="this.src='https://via.placeholder.com/300x200?text=Restaurant'">
                </div>
                <div class="restaurant-info">
                    <h3>${restaurant.name}</h3>
                        <div class="restaurant-rating">
                        <i class="fas fa-star"></i>
                        <span>${restaurant.averageRating.toFixed(1)}</span>
                        <span class="rating-count">(${restaurant.totalReviews})</span>
                    </div>
                    <p class="restaurant-description">${restaurant.description || 'Ресторан с вкусной едой'}</p>
                    <div class="restaurant-footer">
                        <span class="restaurant-delivery">30-45 мин</span>
                        <span class="restaurant-category">${restaurant.category || 'Ресторан'}</span>
                    </div>
                </div>
            </div>
        `).join('');

        // Добавляем обработчики кликов
        container.querySelectorAll('.restaurant-card').forEach(card => {
            card.addEventListener('click', (e) => {
                if (!e.target.closest('.restaurant-card')) return;
                const restaurantId = card.dataset.id;
                this.loadDishes(restaurantId);
                
                // Прокручиваем к блюдам
                const dishesSection = document.querySelector('.dishes-section');
                if (dishesSection) {
                    dishesSection.scrollIntoView({ behavior: 'smooth' });
                }
            });
        });
    }

    // Отображение блюд
    displayDishes() {
        const container = document.getElementById('dishesGrid');
        if (!container) return;

        if (this.filteredDishes.length === 0) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-utensils"></i>
                    <p>Блюда не найдены</p>
                </div>
            `;
            return;
        }

        container.innerHTML = this.filteredDishes.map(dish => `
            <div class="dish-card" data-id="${dish.id}">
                <div class="dish-image">
                    <img src="${dish.imageUrl || 'https://via.placeholder.com/300x200?text=Dish'}" 
                         alt="${dish.name}"
                         onerror="this.src='https://via.placeholder.com/300x200?text=Dish'">
                </div>
                <div class="dish-info">
                    <h3>${dish.name}</h3>
                    <div class="dish-rating">
                        <i class="fas fa-star"></i>
                        <span>${dish.averageRating.toFixed(1)}</span>
                    </div>
                    <p class="dish-description">${dish.description || 'Вкусное блюдо'}</p>
                    <div class="dish-footer">
                        <span class="dish-price">${Utils.formatPrice(dish.price)}</span>
                        <button class="dish-add-btn" data-dish='${JSON.stringify(dish).replace(/'/g, "\\'")}'>
                            <i class="fas fa-plus"></i>
                        </button>
                    </div>
                </div>
            </div>
        `).join('');
        
        // Добавляем обработчики кликов
        container.querySelectorAll('.dish-card').forEach(card => {
            card.addEventListener('click', (e) => {
                if (e.target.closest('.dish-add-btn')) return;
                const dishId = card.dataset.id;
                this.showDishDetails(dishId);
            });
        });
        
        // Обработчики для кнопок добавления в корзину
        container.querySelectorAll('.dish-add-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const dish = JSON.parse(btn.dataset.dish);
                this.addToCart(dish);
            });
        });
    }

    // Показать детали блюда
    async showDishDetails(dishId) {
        const dish = this.dishes.find(d => d.id === dishId) || this.filteredDishes.find(d => d.id === dishId);
        if (!dish) return;
        
        const modalBody = document.getElementById('dishModalBody');
        if (!modalBody) return;
        
        modalBody.innerHTML = `
            <div class="dish-modal-content">
                <div class="dish-modal-image">
                    <img src="${dish.imageUrl || 'https://via.placeholder.com/300x200?text=Dish'}" 
                         alt="${dish.name}"
                         onerror="this.src='https://via.placeholder.com/300x200?text=Dish'">
                </div>
                <div class="dish-modal-info">
                    <h2>${dish.name}</h2>
                    <div class="dish-modal-meta">
                        <span class="dish-price">${Utils.formatPrice(dish.price)}</span>
                        <span class="dish-category">${dish.category || 'Основное блюдо'}</span>
                        <span class="dish-time"><i class="fas fa-clock"></i> ${dish.preparationTime || 15} мин</span>
                    </div>
                    <p class="dish-modal-description">${dish.description || 'Вкусное блюдо от нашего шефа'}</p>
                    
                    <div class="dish-modal-restaurant">
                        <i class="fas fa-store"></i>
                        <span>${dish.restaurantName || 'Ресторан'}</span>
                    </div>
                    
                    <div class="dish-modal-actions">
                        <div class="quantity-control">
                            <button class="quantity-btn minus"><i class="fas fa-minus"></i></button>
                            <span class="quantity-value">1</span>
                            <button class="quantity-btn plus"><i class="fas fa-plus"></i></button>
                        </div>
                        <button class="btn btn-primary add-to-cart-modal">
                            <i class="fas fa-shopping-cart"></i> Добавить в корзину
                        </button>
                    </div>
                </div>
            </div>
        `;
        
        document.getElementById('dishModal').style.display = 'block';
        
        // Обработчики для модального окна
        let quantity = 1;
        const quantityValue = modalBody.querySelector('.quantity-value');
        
        modalBody.querySelector('.minus').addEventListener('click', () => {
            if (quantity > 1) {
                quantity--;
                quantityValue.textContent = quantity;
            }
        });
        
        modalBody.querySelector('.plus').addEventListener('click', () => {
            quantity++;
            quantityValue.textContent = quantity;
        });
        
        modalBody.querySelector('.add-to-cart-modal').addEventListener('click', () => {
            this.addToCart(dish, quantity);
            document.getElementById('dishModal').style.display = 'none';
        });
    }
    
    // Добавление в корзину
    async addToCart(dish, quantity = 1) {
        try {
            await ApiClient.addToCart(dish.id, quantity);
            Utils.showNotification(`${dish.name} добавлен в корзину`, 'success');
            updateCartCount();
        } catch (error) {
            console.error('Error adding to cart:', error);
            
            // Fallback на локальную корзину
            ApiClient.addToLocalCart(dish, quantity);
            Utils.showNotification(`${dish.name} добавлен в корзину`, 'success');
        }
    }
    
    // Поиск блюд
    searchDishes(query) {
        if (!query.trim()) {
            this.filteredDishes = [...this.dishes];
        } else {
            const searchTerm = query.toLowerCase();
            this.filteredDishes = this.dishes.filter(dish =>
                dish.name.toLowerCase().includes(searchTerm) ||
                dish.description.toLowerCase().includes(searchTerm) ||
                (dish.category && dish.category.toLowerCase().includes(searchTerm))
            );
        }
        this.displayDishes();
    }
    
    // Фильтрация по категории
    filterByCategory(category) {
        if (category === 'all') {
            this.filteredDishes = [...this.dishes];
        } else {
            this.filteredDishes = this.dishes.filter(dish =>
                dish.category && dish.category.toLowerCase() === category
            );
        }
        this.displayDishes();
    }
    
    // Сортировка блюд
    sortDishes(sortBy) {
    switch (sortBy) {
        case 'price-asc':
            this.filteredDishes.sort((a, b) => a.price - b.price);
            break;
        case 'price-desc':
            this.filteredDishes.sort((a, b) => b.price - a.price);
            break;
        case 'name':
            this.filteredDishes.sort((a, b) => a.name.localeCompare(b.name, 'ru'));
            break;
        case 'popular':
        default:
            // Сортировка по рейтингу (звездам) - по убыванию
            this.filteredDishes.sort((a, b) => {
                // Используем averageRating из БД
                const ratingA = a.averageRating || 0;
                const ratingB = b.averageRating || 0;
                
                // Сначала по рейтингу
                if (ratingB !== ratingA) {
                    return ratingB - ratingA;
                }
                
                // Если рейтинги равны, сортируем по количеству отзывов
                const reviewsA = a.totalReviews || a.restaurant?.totalReviews || 0;
                const reviewsB = b.totalReviews || b.restaurant?.totalReviews || 0;
                
                return reviewsB - reviewsA;
            });
            break;
    }
    this.displayDishes();
}
    
    // Отображение ошибки
    displayError(containerId, message) {
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = `
                <div class="error-message">
                    <i class="fas fa-exclamation-circle"></i>
                    <p>${message}</p>
                    <button class="btn btn-outline retry-btn">Повторить</button>
                </div>
            `;
            
            container.querySelector('.retry-btn').addEventListener('click', () => {
                if (containerId === 'restaurantsGrid') {
                    this.loadRestaurants();
                } else {
                    this.loadDishes();
                }
            });
        }
    }
    
    // Заглушки для тестирования
    getMockRestaurants() {
        return [
            {
                id: '1',
                name: 'Пиццерия "Италия"',
                description: 'Настоящая итальянская пицца и паста',
                imageUrl: 'https://via.placeholder.com/300x200?text=Italian+Pizza',
                category: 'Итальянская'
            },
            {
                id: '2',
                name: 'Бургерная "Американская"',
                description: 'Сочные бургеры и картошка фри',
                imageUrl: 'https://via.placeholder.com/300x200?text=American+Burgers',
                category: 'Фастфуд'
            },
            {
                id: '3',
                name: 'Суши-бар "Сакура"',
                description: 'Свежие суши и роллы',
                imageUrl: 'https://via.placeholder.com/300x200?text=Japanese+Sushi',
                category: 'Японская'
            }
        ];
    }
    
    getMockDishes() {
        return [
            {
                id: '1',
                name: 'Пицца Маргарита',
                description: 'Классическая итальянская пицца с томатным соусом и моцареллой',
                price: 450,
                imageUrl: 'https://via.placeholder.com/300x200?text=Margarita+Pizza',
                category: 'pizza',
                restaurantId: '1',
                restaurantName: 'Пиццерия "Италия"',
                preparationTime: 20
            },
            {
                id: '2',
                name: 'Чизбургер',
                description: 'Сочная говяжья котлета с сыром чеддер и свежими овощами',
                price: 350,
                imageUrl: 'https://via.placeholder.com/300x200?text=Cheeseburger',
                category: 'burger',
                restaurantId: '2',
                restaurantName: 'Бургерная "Американская"',
                preparationTime: 15
            },
            {
                id: '3',
                name: 'Филадельфия',
                description: 'Ролл с лососем, сливочным сыром и огурцом',
                price: 550,
                imageUrl: 'https://via.placeholder.com/300x200?text=Philadelphia+Roll',
                category: 'sushi',
                restaurantId: '3',
                restaurantName: 'Суши-бар "Сакура"',
                preparationTime: 10
            },
            {
                id: '4',
                name: 'Цезарь с курицей',
                description: 'Салат с куриной грудкой, листьями салата, сухариками и соусом цезарь',
                price: 320,
                imageUrl: 'https://via.placeholder.com/300x200?text=Caesar+Salad',
                category: 'salad',
                restaurantId: '1',
                restaurantName: 'Пиццерия "Италия"',
                preparationTime: 10
            }
        ];
    }
}

// Инициализация менеджера меню
const menuManager = new MenuManager();

// Глобальные функции для использования в HTML
function loadRestaurants() {
    menuManager.loadRestaurants();
}

function loadDishes() {
    menuManager.loadDishes();
}

function searchDishes(query) {
    menuManager.searchDishes(query);
}

function filterByCategory(category) {
    menuManager.filterByCategory(category);
}

function sortDishes(sortBy) {
    menuManager.sortDishes(sortBy);
}

// Экспорт для использования в других файлах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        MenuManager,
        menuManager,
        loadRestaurants,
        loadDishes,
        searchDishes,
        filterByCategory,
        sortDishes
    };
}