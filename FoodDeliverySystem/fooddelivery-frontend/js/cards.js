// cards.js - Управление сохраненными картами
class CardsManager {
    constructor() {
        this.cards = [];
        this.defaultCard = null;
        this.initialized = false;
    }

    async loadUserCards() {
        try {
            const container = document.getElementById('savedCardsList');
            if (container) {
                container.innerHTML = `
                    <div class="loading-cards">
                        <div class="spinner"></div>
                        <p>Загружаем сохраненные карты...</p>
                    </div>
                `;
            }
            
            // Проверяем, авторизован ли пользователь
            const token = Utils.getToken();
            if (!token) {
                throw new Error('Пользователь не авторизован');
            }
            
            const response = await ApiClient.getPaymentCards();
            
            if (response && response.success) {
                this.cards = response.data || [];
                
                // Находим карту по умолчанию
                this.defaultCard = this.cards.find(card => card.isActive) || null;
                
                this.renderCardsList();
                this.updateCardSelectors();
                
                this.initialized = true;
                return this.cards;
            } else {
                throw new Error('Не удалось загрузить карты');
            }
            
        } catch (error) {
            console.error('Error loading user cards:', error);
            
            const container = document.getElementById('savedCardsList');
            if (container) {
                container.innerHTML = `
                    <div class="no-cards-message">
                        <i class="fas fa-exclamation-circle"></i>
                        <p>Не удалось загрузить карты</p>
                        <p>Попробуйте обновить страницу</p>
                    </div>
                `;
            }
            
            this.cards = [];
            return [];
        }
    }

    renderCardsList() {
        const container = document.getElementById('savedCardsList');
        if (!container) return;

        if (this.cards.length === 0) {
            container.innerHTML = `
                <div class="no-cards-message">
                    <i class="fas fa-credit-card"></i>
                    <p>У вас нет сохраненных карт</p>
                    <p>Добавьте карту при оформлении заказа</p>
                </div>
            `;
            return;
        }

        container.innerHTML = this.cards.map(card => `
            <div class="saved-card-item ${card.isActive ? 'active' : ''}" data-card-id="${card.id}">
                <div class="card-info">
                    <div class="card-type-icon">
                        <i class="fab fa-cc-visa"></i>
                    </div>
                    <div class="card-details">
                        <h4>Карта •••• ${card.cardLastFourDigits}</h4>
                        <p>${card.cardHolderName}</p>
                        <p>Действует до: ${card.expiry}</p>
                    </div>
                </div>
                <div class="card-actions">
                    ${card.isActive ? 
                        '<span class="default-badge"><i class="fas fa-check-circle"></i> По умолчанию</span>' : 
                        `<button class="btn btn-text set-default-card-btn" data-card-id="${card.id}">
                            Сделать основной
                        </button>`
                    }
                    <button class="btn btn-text delete-card-btn" data-card-id="${card.id}">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');

        // Добавляем обработчики событий
        this.addCardEventListeners();
    }

    updateCardSelectors() {
        // Обновляем радиокнопки в checkout
        const radioContainer = document.getElementById('savedCardsRadio');
        if (radioContainer) {
            if (this.cards.length === 0) {
                radioContainer.innerHTML = `
                    <div class="no-saved-cards">
                        <p>Нет сохраненных карт</p>
                    </div>
                `;
            } else {
                radioContainer.innerHTML = this.cards.map(card => `
                    <div class="saved-card-option">
                        <label class="radio-label">
                            <input type="radio" name="savedCard" 
                                   value="${card.id}" 
                                   ${card.isActive ? 'checked' : ''}
                                   class="saved-card-radio">
                            <div class="card-option-content">
                                <div class="card-option-icon">
                                    <i class="fab fa-cc-visa"></i>
                                </div>
                                <div class="card-option-details">
                                    <h4>Карта •••• ${card.cardLastFourDigits}</h4>
                                    <p>${card.cardHolderName} • Действует до: ${card.expiry}</p>
                                </div>
                                ${card.isActive ? 
                                    '<span class="default-badge-sm">Основная</span>' : ''}
                            </div>
                        </label>
                    </div>
                `).join('') + `
                    <div class="saved-card-option">
                        <label class="radio-label">
                            <input type="radio" name="savedCard" 
                                   value="new" 
                                   class="saved-card-radio">
                            <div class="card-option-content">
                                <div class="card-option-icon">
                                    <i class="fas fa-plus"></i>
                                </div>
                                <div class="card-option-details">
                                    <h4>Использовать новую карту</h4>
                                    <p>Введите данные новой карты</p>
                                </div>
                            </div>
                        </label>
                    </div>
                `;
            }
        }
    }

    addCardEventListeners() {
        // Установка карты по умолчанию
        document.querySelectorAll('.set-default-card-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const cardId = e.target.dataset.cardId;
                await this.setDefaultCard(cardId);
            });
        });

        // Удаление карты
        document.querySelectorAll('.delete-card-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                const cardId = e.target.dataset.cardId;
                if (confirm('Удалить эту карту?')) {
                    await this.deleteCard(cardId);
                }
            });
        });

        // Переключение между сохраненными картами и новой
        document.querySelectorAll('.saved-card-radio').forEach(radio => {
            radio.addEventListener('change', (e) => {
                this.handleCardSelection(e.target.value);
            });
        });

        // Изменение выбора в селекте
        const cardSelect = document.getElementById('savedCardSelect');
        if (cardSelect) {
            cardSelect.addEventListener('change', (e) => {
                this.handleCardSelection(e.target.value);
            });
        }
    }

    handleCardSelection(cardId) {
        const cardForm = document.getElementById('cardForm');
        const savedCardCvv = document.getElementById('savedCardCvv');
        
        if (cardId === 'new') {
            // Показать форму для новой карты
            if (cardForm) cardForm.style.display = 'block';
            if (savedCardCvv) savedCardCvv.style.display = 'none';
            
            // Сбросить форму новой карты
            document.getElementById('cardNumber').value = '';
            document.getElementById('cardExpiry').value = '';
            document.getElementById('cardCVC').value = '';
            document.getElementById('cardHolder').value = '';
            document.getElementById('saveCard').checked = true;
        } else {
            // Использовать сохраненную карту
            if (cardForm) cardForm.style.display = 'none';
            if (savedCardCvv) savedCardCvv.style.display = 'block';
            
            // Найти выбранную карту
            const selectedCard = this.cards.find(c => c.id === cardId);
            if (selectedCard) {
                // Можно показать информацию о карте
                const cardInfo = document.getElementById('selectedCardInfo');
                if (cardInfo) {
                    cardInfo.innerHTML = `
                        <p><strong>Используется карта:</strong> •••• ${selectedCard.cardLastFourDigits}</p>
                        <p>${selectedCard.cardHolderName} (до ${selectedCard.expiry})</p>
                    `;
                }
            }
        }
    }

    async setDefaultCard(cardId) {
        try {
            const response = await ApiClient.setDefaultCard(cardId);
            if (response.success) {
                // Обновляем локальные данные
                this.cards.forEach(card => {
                    card.isActive = card.id === cardId;
                });
                this.defaultCard = this.cards.find(c => c.id === cardId);
                
                // Обновляем отображение
                this.renderCardsList();
                this.updateCardSelectors();
                
                Utils.showNotification('Основная карта обновлена', 'success');
            }
        } catch (error) {
            console.error('Error setting default card:', error);
            Utils.showNotification('Ошибка обновления карты', 'error');
        }
    }

    async deleteCard(cardId) {
        try {
            const response = await ApiClient.deletePaymentCard(cardId);
            if (response.success) {
                // Удаляем из локального списка
                this.cards = this.cards.filter(card => card.id !== cardId);
                
                // Если удалили карту по умолчанию, выбираем новую
                if (this.defaultCard && this.defaultCard.id === cardId) {
                    this.defaultCard = this.cards.length > 0 ? this.cards[0] : null;
                    if (this.defaultCard) {
                        await this.setDefaultCard(this.defaultCard.id);
                    }
                }
                
                // Обновляем отображение
                this.renderCardsList();
                this.updateCardSelectors();
                
                Utils.showNotification('Карта удалена', 'success');
            }
        } catch (error) {
            console.error('Error deleting card:', error);
            Utils.showNotification('Ошибка удаления карты', 'error');
        }
    }

    getSelectedCardData() {
        const selectedCardId = this.getSelectedCardId();
        
        if (selectedCardId === 'new' || !selectedCardId) {
            return {
                type: 'new',
                data: null
            };
        }
        
        const card = this.cards.find(c => c.id === selectedCardId);
        return {
            type: 'saved',
            data: card
        };
    }

    getSelectedCardId() {
        // Проверяем радиокнопки
        const selectedRadio = document.querySelector('.saved-card-radio:checked');
        if (selectedRadio) {
            return selectedRadio.value;
        }
        
        // Проверяем селект
        const cardSelect = document.getElementById('savedCardSelect');
        if (cardSelect) {
            return cardSelect.value;
        }
        
        return null;
    }

    async processPaymentWithSavedCard(orderId, amount, cvv = null) {
        const selectedCard = this.getSelectedCardData();
        
        if (selectedCard.type === 'new') {
            // Используем обычный метод оплаты
            return await ApiClient.processPayment({
                orderId: orderId,
                amount: amount,
                cardId: null
            });
        } else {
            // Используем сохраненную карту
            return await ApiClient.payWithCard(
                orderId,
                amount,
                selectedCard.data.id,
                cvv
            );
        }
    }
}

// Инициализация менеджера карт
const cardsManager = new CardsManager();

// Глобальные функции
async function loadUserCards() {
    return await cardsManager.loadUserCards();
}

function handleCardSelection(cardId) {
    cardsManager.handleCardSelection(cardId);
}

async function setDefaultCard(cardId) {
    return await cardsManager.setDefaultCard(cardId);
}

async function deleteCard(cardId) {
    return await cardsManager.deleteCard(cardId);
}

async function processPaymentWithSavedCard(orderId, amount, cvv = null) {
    return await cardsManager.processPaymentWithSavedCard(orderId, amount, cvv);
}

// Экспорт для использования в других модулях
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        CardsManager,
        cardsManager,
        loadUserCards,
        handleCardSelection,
        setDefaultCard,
        deleteCard,
        processPaymentWithSavedCard
    };
}