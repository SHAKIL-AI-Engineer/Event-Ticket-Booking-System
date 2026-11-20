import { Checkout } from "@/components/checkout";
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <Checkout id={id} />;
}
