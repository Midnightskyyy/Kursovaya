// cards-simple.js - Упрощенная версия CardsManager
class CardsManager {
    constructor() {
        this.cards = [];
        this.defaultCard = null;
    }
    
    async loadUserCards() {
        try {
            console.log('Loading user cards...');
            const response = await ApiClient.getPaymentCards();
            
            if (response && response.success) {
                this.cards = response.data || [];
                this.defaultCard = this.cards.find(card => card.isActive) || null;
                
                console.log('Loaded cards:', this.cards.length);
                return this.cards;
            } else {
                console.log('No cards found');
                return [];
            }
        } catch (error) {
            console.log('Error loading cards (might not be logged in):', error);
            return [];
        }
    }
    
    hasCards() {
        return this.cards.length > 0;
    }
    
    getCards() {
        return this.cards;
    }
    
    getDefaultCard() {
        return this.defaultCard;
    }
    
    getCardById(cardId) {
        return this.cards.find(card => card.id === cardId);
    }
    
    getCardIcon(cardType) {
        const icons = {
            'visa': 'fab fa-cc-visa',
            'mastercard': 'fab fa-cc-mastercard',
            'mir': 'fab fa-cc-mir',
            'amex': 'fab fa-cc-amex',
            'default': 'far fa-credit-card'
        };
        return icons[cardType?.toLowerCase()] || icons.default;
    }
}

// Глобальная инициализация
window.CardsManager = CardsManager;
window.initCardsManager = function() {
    return new CardsManager();
};