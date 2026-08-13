import { Outlet, useParams } from 'react-router-dom';
import { CartProvider } from '../lib/cart';

/** Gives every page under /:slug a cart scoped to that store. */
export default function StoreLayout() {
  const { slug = '' } = useParams();
  return (
    <CartProvider slug={slug}>
      <Outlet />
    </CartProvider>
  );
}
